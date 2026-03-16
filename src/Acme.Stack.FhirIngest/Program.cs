using System.Text.Json;
using Acme.Stack.Core;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddOpenApi();

// Register NpgsqlDataSource for DI — connection string injected by Aspire via env var
var connectionString = builder.Configuration.GetConnectionString("doltgresql");
if (!string.IsNullOrEmpty(connectionString))
{
    var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
    builder.Services.AddSingleton(dataSourceBuilder.Build());
}

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapDefaultEndpoints();

// Create schema on startup — idempotent via IF NOT EXISTS
var dbSource = app.Services.GetService<NpgsqlDataSource>();
if (dbSource is not null)
{
    try
    {
        await using var conn = await dbSource.OpenConnectionAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = Schema.CreatePatients;
        await cmd.ExecuteNonQueryAsync();
        cmd.CommandText = Schema.CreateObservations;
        await cmd.ExecuteNonQueryAsync();
        app.Logger.LogInformation("Database schema initialized (patients, observations)");
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Failed to initialize database schema — DB may be unavailable");
    }
}

// POST /fhir/Bundle — Accept a Synthea FHIR R4 Bundle, extract and persist Patient + Observation
app.MapPost("/fhir/Bundle", async (HttpRequest request, ILogger<Program> logger) =>
{
    var db = request.HttpContext.RequestServices.GetService<NpgsqlDataSource>();
    // Read raw JSON from request body
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

    // FR-032: DoltgreSQL unreachable returns HTTP 503
    NpgsqlConnection conn;
    try
    {
        conn = await db.OpenConnectionAsync();
    }
    catch (NpgsqlException ex)
    {
        logger.LogError(ex, "DoltgreSQL unreachable");
        return Results.Problem(
            detail: "Database is unavailable",
            statusCode: 503);
    }

    await using (conn)
    {
        // Persist patients — UPSERT for idempotent ingestion
        foreach (var patient in patients)
        {
            var record = MapPatient(patient);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO patients (id, family_name, given_name, birth_date, gender)
                VALUES ($1, $2, $3, $4, $5)
                ON CONFLICT (id) DO UPDATE SET
                    family_name = EXCLUDED.family_name,
                    given_name = EXCLUDED.given_name,
                    birth_date = EXCLUDED.birth_date,
                    gender = EXCLUDED.gender
                """;
            cmd.Parameters.AddWithValue(record.Id);
            cmd.Parameters.AddWithValue(record.FamilyName ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue(record.GivenName ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue(record.BirthDate.HasValue
                ? record.BirthDate.Value
                : DBNull.Value);
            cmd.Parameters.AddWithValue(record.Gender ?? (object)DBNull.Value);
            await cmd.ExecuteNonQueryAsync();
        }

        // Persist observations — UPSERT for idempotent ingestion
        foreach (var obs in observations)
        {
            var record = MapObservation(obs);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO observations (id, patient_id, code, display, value, unit, effective_date)
                VALUES ($1, $2, $3, $4, $5, $6, $7)
                ON CONFLICT (id) DO UPDATE SET
                    patient_id = EXCLUDED.patient_id,
                    code = EXCLUDED.code,
                    display = EXCLUDED.display,
                    value = EXCLUDED.value,
                    unit = EXCLUDED.unit,
                    effective_date = EXCLUDED.effective_date
                """;
            cmd.Parameters.AddWithValue(record.Id);
            cmd.Parameters.AddWithValue(record.PatientId ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue(record.Code ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue(record.Display ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue(record.Value.HasValue
                ? record.Value.Value
                : DBNull.Value);
            cmd.Parameters.AddWithValue(record.Unit ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue(record.EffectiveDate);
            await cmd.ExecuteNonQueryAsync();
        }

        // Commit the data change in DoltgreSQL for version tracking
        string? commitHash = null;
        try
        {
            await using var commitCmd = conn.CreateCommand();
            var timestamp = DateTime.UtcNow.ToString("o");
            commitCmd.CommandText = "SELECT DOLT_COMMIT('-Am', $1)";
            commitCmd.Parameters.AddWithValue($"Ingest: FHIR Bundle at {timestamp}");
            var result = await commitCmd.ExecuteScalarAsync();
            commitHash = result?.ToString();
            logger.LogInformation("DoltgreSQL commit: {CommitHash}", commitHash);
        }
        catch (Exception ex)
        {
            // Log but don't fail the request — data is persisted, versioning is best-effort
            logger.LogWarning(ex, "DOLT_COMMIT failed — data persisted but not versioned");
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

// Map Firely Patient to flat PatientRecord
static PatientRecord MapPatient(Patient patient)
{
    var name = patient.Name.FirstOrDefault();
    return new PatientRecord(
        Id: patient.Id,
        FamilyName: name?.Family ?? "",
        GivenName: name?.Given.FirstOrDefault() ?? "",
        BirthDate: DateOnly.TryParse(patient.BirthDate, out var bd) ? bd : null,
        Gender: patient.Gender?.ToString()?.ToLowerInvariant() ?? "unknown"
    );
}

// Map Firely Observation to flat ObservationRecord
static ObservationRecord MapObservation(Observation obs)
{
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

    return new ObservationRecord(
        Id: obs.Id,
        PatientId: patientRef,
        Code: coding?.Code ?? "",
        Display: coding?.Display ?? "",
        Value: value,
        Unit: unit,
        EffectiveDate: effectiveDate
    );
}

// Make Program class accessible to test project via WebApplicationFactory
public partial class Program { }
