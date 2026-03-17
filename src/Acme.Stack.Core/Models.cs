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
/// SQL schema constants for Dolt MySQL table creation.
/// Each service creates its own tables on startup with CREATE TABLE IF NOT EXISTS.
/// </summary>
public static class Schema
{
    public const string CreatePatients = """
        CREATE TABLE IF NOT EXISTS patients (
            id VARCHAR(255) PRIMARY KEY,
            family_name VARCHAR(255),
            given_name VARCHAR(255),
            birth_date DATE,
            gender VARCHAR(50),
            ingested_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
        )
        """;

    public const string CreateObservations = """
        CREATE TABLE IF NOT EXISTS observations (
            id VARCHAR(255) PRIMARY KEY,
            patient_id VARCHAR(255),
            code VARCHAR(255),
            display VARCHAR(500),
            value DECIMAL(18,4),
            unit VARCHAR(100),
            effective_date DATETIME,
            ingested_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
            CONSTRAINT fk_patient FOREIGN KEY (patient_id) REFERENCES patients(id)
        )
        """;
}
