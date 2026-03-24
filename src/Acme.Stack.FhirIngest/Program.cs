using System.Text.Json;
using Acme.Stack.Core;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using MySqlConnector;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddOpenApi();

// Register MySqlDataSource for DI — connection string injected by Aspire via env var
var connectionString = builder.Configuration.GetConnectionString("acme-health");
if (!string.IsNullOrEmpty(connectionString))
{
    var dataSourceBuilder = new MySqlDataSourceBuilder(connectionString);
    builder.Services.AddSingleton<MySqlDataSource>(dataSourceBuilder.Build());
}

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapDefaultEndpoints();

// Create schema on startup — all tables in dependency order (ADR-2005)
var dbSource = app.Services.GetService<MySqlDataSource>();
if (dbSource is not null)
{
    try
    {
        await using var conn = await dbSource.OpenConnectionAsync();
        foreach (var ddl in Schema.AllTables)
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = ddl;
            await cmd.ExecuteNonQueryAsync();
        }
        app.Logger.LogInformation(
            "Database schema initialized ({TableCount} tables)", Schema.AllTables.Length);
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Failed to initialize database schema — DB may be unavailable");
    }
}

// POST /fhir/Bundle — Accept a Synthea FHIR R4 Bundle, extract and persist Patient + Observation
app.MapPost("/fhir/Bundle", async (HttpRequest request, ILogger<Program> logger) =>
{
    var db = request.HttpContext.RequestServices.GetService<MySqlDataSource>();
    // Read raw JSON from request body — preserved for raw_payloads archive
    using var reader = new StreamReader(request.Body);
    var json = await reader.ReadToEndAsync();

    // Deserialize with Firely-aware options (NOT standard System.Text.Json)
    Bundle bundle;
    try
    {
        var options = new JsonSerializerOptions().ForFhir(ModelInfo.ModelInspector);
        bundle = JsonSerializer.Deserialize<Bundle>(json, options)
            ?? throw new InvalidOperationException("Deserialized bundle is null");
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Failed to parse FHIR Bundle");
        return Results.Problem(
            detail: $"Invalid FHIR Bundle JSON: {ex.Message}",
            statusCode: 422);
    }

    // Extract typed resources from Bundle entries
    var patients = bundle.Entry
        .Where(e => e.Resource is Patient)
        .Select(e => (Patient)e.Resource)
        .ToList();

    var observations = bundle.Entry
        .Where(e => e.Resource is Observation)
        .Select(e => (Observation)e.Resource)
        .ToList();

    if (patients.Count == 0)
    {
        return Results.Problem(
            detail: "No Patient resources found in Bundle",
            statusCode: 422);
    }

    logger.LogInformation("Parsed Bundle: {PatientCount} patients, {ObservationCount} observations",
        patients.Count, observations.Count);

    if (db is null)
    {
        return Results.Problem(
            detail: "Database is not configured",
            statusCode: 503);
    }

    // FR-013: Dolt MySQL unreachable returns HTTP 503
    MySqlConnection conn;
    try
    {
        conn = await db.OpenConnectionAsync();
    }
    catch (MySqlException ex)
    {
        logger.LogError(ex, "Dolt MySQL unreachable");
        return Results.Problem(
            detail: "Database is unavailable",
            statusCode: 503);
    }

    await using (conn)
    {
        await using var transaction = await conn.BeginTransactionAsync();

        // --- Layer 2: Archive raw payload (ADR-2005) ---
        var payloadHash = PayloadHash.Compute(json);
        var rawPayloadId = Guid.NewGuid().ToString();
        {
            await using var cmd = conn.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = """
                INSERT IGNORE INTO raw_payloads (id, content_type, source_standard, source_version, payload, payload_hash)
                VALUES (@id, @contentType, @sourceStandard, @sourceVersion, @payload, @payloadHash)
                """;
            cmd.Parameters.AddWithValue("@id", rawPayloadId);
            cmd.Parameters.AddWithValue("@contentType", "application/fhir+json");
            cmd.Parameters.AddWithValue("@sourceStandard", "fhir-r4");
            cmd.Parameters.AddWithValue("@sourceVersion", "R4/4.0.1");
            cmd.Parameters.AddWithValue("@payload", json);
            cmd.Parameters.AddWithValue("@payloadHash", payloadHash);
            await cmd.ExecuteNonQueryAsync();
        }

        // Resolve the actual raw_payload ID (may differ if INSERT IGNORE skipped a duplicate)
        {
            await using var lookupCmd = conn.CreateCommand();
            lookupCmd.Transaction = transaction;
            lookupCmd.CommandText = "SELECT id FROM raw_payloads WHERE payload_hash = @hash LIMIT 1";
            lookupCmd.Parameters.AddWithValue("@hash", payloadHash);
            rawPayloadId = (string?)await lookupCmd.ExecuteScalarAsync()
                ?? throw new InvalidOperationException("raw_payload lookup failed after archive insert");
        }

        // --- Layer 1: Persist patients with source tracking ---
        foreach (var patient in patients)
        {
            var record = MapPatient(patient);
            await using var cmd = conn.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = """
                INSERT INTO patients (id, family_name, given_name, birth_date, gender, source_standard, source_version)
                VALUES (@id, @familyName, @givenName, @birthDate, @gender, @sourceStandard, @sourceVersion)
                AS new
                ON DUPLICATE KEY UPDATE
                    family_name = new.family_name,
                    given_name = new.given_name,
                    birth_date = new.birth_date,
                    gender = new.gender,
                    source_standard = new.source_standard,
                    source_version = new.source_version
                """;
            cmd.Parameters.AddWithValue("@id", record.Id);
            cmd.Parameters.AddWithValue("@familyName", record.FamilyName ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@givenName", record.GivenName ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@birthDate", record.BirthDate.HasValue
                ? record.BirthDate.Value
                : DBNull.Value);
            cmd.Parameters.AddWithValue("@gender", record.Gender ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@sourceStandard", record.SourceStandard);
            cmd.Parameters.AddWithValue("@sourceVersion", record.SourceVersion);
            await cmd.ExecuteNonQueryAsync();

            // --- Layer 3: Provenance for patient ---
            await InsertProvenance(conn, transaction, "patients", record.Id, rawPayloadId, "fhir-r4-parser");
        }

        // --- Layer 1: Persist observations as health_records (ADR-2005) ---
        foreach (var obs in observations)
        {
            var record = MapObservation(obs);
            await using var cmd = conn.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = """
                INSERT INTO health_records
                    (id, patient_id, record_type, code, code_system, display,
                     value_numeric, value_text, unit, device_type, effective_date,
                     source_standard, source_version)
                VALUES
                    (@id, @patientId, @recordType, @code, @codeSystem, @display,
                     @valueNumeric, @valueText, @unit, @deviceType, @effectiveDate,
                     @sourceStandard, @sourceVersion)
                AS new
                ON DUPLICATE KEY UPDATE
                    patient_id = new.patient_id,
                    code = new.code,
                    code_system = new.code_system,
                    display = new.display,
                    value_numeric = new.value_numeric,
                    value_text = new.value_text,
                    unit = new.unit,
                    effective_date = new.effective_date,
                    source_standard = new.source_standard,
                    source_version = new.source_version
                """;
            cmd.Parameters.AddWithValue("@id", record.Id);
            cmd.Parameters.AddWithValue("@patientId", record.PatientId ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@recordType", record.RecordType);
            cmd.Parameters.AddWithValue("@code", record.Code ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@codeSystem", record.CodeSystem ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@display", record.Display ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@valueNumeric", record.ValueNumeric.HasValue
                ? record.ValueNumeric.Value
                : DBNull.Value);
            cmd.Parameters.AddWithValue("@valueText", record.ValueText ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@unit", record.Unit ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@deviceType", "ehr");
            cmd.Parameters.AddWithValue("@effectiveDate", record.EffectiveDate.HasValue
                ? record.EffectiveDate.Value
                : DBNull.Value);
            cmd.Parameters.AddWithValue("@sourceStandard", record.SourceStandard);
            cmd.Parameters.AddWithValue("@sourceVersion", record.SourceVersion);
            await cmd.ExecuteNonQueryAsync();

            // --- Layer 3: Provenance for health record ---
            await InsertProvenance(conn, transaction, "health_records", record.Id, rawPayloadId, "fhir-r4-parser");
        }

        await transaction.CommitAsync();

        // Commit the data change in Dolt MySQL for version tracking
        string? commitHash = null;
        try
        {
            await using var commitCmd = conn.CreateCommand();
            var timestamp = DateTime.UtcNow.ToString("o");
            commitCmd.CommandText = "CALL DOLT_COMMIT('-Am', @msg)";
            commitCmd.Parameters.AddWithValue("@msg", $"Ingest: FHIR Bundle at {timestamp}");
            await using var commitReader = await commitCmd.ExecuteReaderAsync();
            if (await commitReader.ReadAsync())
                commitHash = commitReader.GetString(0);
            logger.LogInformation("Dolt commit: {CommitHash}", commitHash);
        }
        catch (Exception ex)
        {
            // Log but don't fail the request — data is persisted, versioning is best-effort
            logger.LogWarning(ex, "DOLT_COMMIT failed — data persisted but not versioned");
        }

        // Backfill dolt_commit into provenance records for this ingest
        if (commitHash is not null)
        {
            try
            {
                await using var updateCmd = conn.CreateCommand();
                updateCmd.CommandText = """
                    UPDATE provenance SET dolt_commit = @hash
                    WHERE raw_payload_id = @rawId AND dolt_commit IS NULL
                    """;
                updateCmd.Parameters.AddWithValue("@hash", commitHash);
                updateCmd.Parameters.AddWithValue("@rawId", rawPayloadId);
                await updateCmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to backfill dolt_commit into provenance");
            }
        }

        return Results.Ok(new
        {
            patients = patients.Count,
            observations = observations.Count,
            dolt_commit = commitHash
        });
    }
});

app.Run();

// --- Helper: Insert provenance record ---
// Deterministic ID from (targetId, rawPayloadId) for idempotent re-ingestion
static async System.Threading.Tasks.Task InsertProvenance(
    MySqlConnection conn, MySqlTransaction transaction,
    string targetTable, string targetId, string rawPayloadId, string transform)
{
    // Deterministic ID from natural key — same inputs always produce same provenance ID
    var hash = System.Security.Cryptography.SHA256.HashData(
        System.Text.Encoding.UTF8.GetBytes($"prov:{targetId}:{rawPayloadId}"));
    var provenanceId = new Guid(hash.AsSpan(0, 16)).ToString();

    await using var cmd = conn.CreateCommand();
    cmd.Transaction = transaction;
    cmd.CommandText = """
        INSERT IGNORE INTO provenance (id, target_table, target_id, raw_payload_id, transform)
        VALUES (@id, @targetTable, @targetId, @rawPayloadId, @transform)
        """;
    cmd.Parameters.AddWithValue("@id", provenanceId);
    cmd.Parameters.AddWithValue("@targetTable", targetTable);
    cmd.Parameters.AddWithValue("@targetId", targetId);
    cmd.Parameters.AddWithValue("@rawPayloadId", rawPayloadId);
    cmd.Parameters.AddWithValue("@transform", transform);
    await cmd.ExecuteNonQueryAsync();
}

// Map Firely Patient to flat PatientRecord (with source tracking)
static PatientRecord MapPatient(Patient patient)
{
    if (string.IsNullOrEmpty(patient.Id))
        throw new InvalidOperationException("Patient resource has no Id");

    var name = patient.Name?.FirstOrDefault();
    return new PatientRecord(
        Id: patient.Id,
        FamilyName: name?.Family ?? "",
        GivenName: name?.Given.FirstOrDefault() ?? "",
        BirthDate: DateOnly.TryParse(patient.BirthDate, out var bd) ? bd : null,
        Gender: patient.Gender?.ToString()?.ToLowerInvariant() ?? "unknown"
    );
}

// Map Firely Observation to unified HealthRecord (ADR-2005)
static HealthRecord MapObservation(Observation obs)
{
    if (string.IsNullOrEmpty(obs.Id))
        throw new InvalidOperationException("Observation resource has no Id");

    var coding = obs.Code?.Coding?.FirstOrDefault();
    decimal? value = null;
    string unit = "";

    if (obs.Value is Quantity qty)
    {
        value = qty.Value;
        unit = qty.Unit ?? "";
    }

    // Extract patient reference — Synthea uses "urn:uuid:..." or "Patient/id"
    var patientRef = obs.Subject?.Reference ?? "";
    if (patientRef.StartsWith("urn:uuid:"))
        patientRef = patientRef["urn:uuid:".Length..];
    else if (patientRef.StartsWith("Patient/"))
        patientRef = patientRef["Patient/".Length..];

    // Parse effective date — handle both dateTime and Period
    DateTime effectiveDate = DateTime.UtcNow;
    if (obs.Effective is FhirDateTime fdt && fdt.ToDateTimeOffset(TimeSpan.Zero) is DateTimeOffset dto)
        effectiveDate = dto.UtcDateTime;
    else if (obs.Effective is Period period && period.StartElement?.ToDateTimeOffset(TimeSpan.Zero) is DateTimeOffset pdto)
        effectiveDate = pdto.UtcDateTime;

    return new HealthRecord(
        Id: obs.Id,
        PatientId: patientRef,
        RecordType: "observation",
        Code: coding?.Code ?? "",
        CodeSystem: coding?.System ?? "http://loinc.org",
        Display: coding?.Display ?? "",
        ValueNumeric: value,
        ValueText: null,
        Unit: unit,
        DeviceName: null,
        DeviceType: "ehr",
        EffectiveDate: effectiveDate,
        SourceStandard: "fhir-r4",
        SourceVersion: "R4/4.0.1"
    );
}

// Make Program class accessible to test project via WebApplicationFactory
public partial class Program { }
