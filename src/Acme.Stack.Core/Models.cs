namespace Acme.Stack.Core;

/// <summary>
/// Flat representation of a FHIR Patient resource for database persistence.
/// Mapped from Hl7.Fhir.Model.Patient by the ingest service.
/// </summary>
public record PatientRecord(
    string Id,
    string FamilyName,
    string GivenName,
    DateOnly? BirthDate,
    string Gender
);

/// <summary>
/// Flat representation of a FHIR Observation resource for database persistence.
/// Mapped from Hl7.Fhir.Model.Observation by the ingest service.
/// </summary>
public record ObservationRecord(
    string Id,
    string PatientId,
    string Code,
    string Display,
    decimal? Value,
    string Unit,
    DateTime EffectiveDate
);

/// <summary>
/// SQL schema constants for DoltgreSQL table creation.
/// Each service creates its own tables on startup with CREATE TABLE IF NOT EXISTS.
/// </summary>
public static class Schema
{
    public const string CreatePatients = """
        CREATE TABLE IF NOT EXISTS patients (
            id TEXT PRIMARY KEY,
            family_name TEXT,
            given_name TEXT,
            birth_date DATE,
            gender TEXT,
            ingested_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
        )
        """;

    public const string CreateObservations = """
        CREATE TABLE IF NOT EXISTS observations (
            id TEXT PRIMARY KEY,
            patient_id TEXT REFERENCES patients(id),
            code TEXT,
            display TEXT,
            value DECIMAL,
            unit TEXT,
            effective_date TIMESTAMP,
            ingested_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
        )
        """;
}
