using System.Security.Cryptography;
using System.Text;

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
    string Gender,
    string SourceStandard = "fhir-r4",
    string SourceVersion = "R4/4.0.1"
);

/// <summary>
/// Unified health measurement record. Replaces per-type tables (observations, cgm_readings)
/// with a single table differentiated by record_type and code_system.
/// See ADR-2005: Multi-Standard Schema with Raw Payload Archive.
/// </summary>
public record HealthRecord(
    string Id,
    string? PatientId,
    string RecordType,       // 'observation', 'cgm', 'heart-rate', 'steps', 'sleep'
    string Code,             // LOINC code, Apple HK type, device-specific code
    string CodeSystem,       // 'http://loinc.org', 'apple-healthkit', 'custom/cgm'
    string Display,
    decimal? ValueNumeric,
    string? ValueText,
    string Unit,
    string? DeviceName,      // 'Dexcom G6', 'Apple Watch Series 9', 'Epic'
    string? DeviceType,      // 'cgm', 'smartwatch', 'ehr', 'manual'
    DateTime? EffectiveDate,
    string SourceStandard,   // 'fhir-r4', 'fhir-r5', 'hl7v2', 'apple-health'
    string SourceVersion     // 'R4/4.0.1', 'v2.5.1', 'export-14.0'
);

/// <summary>
/// SQL schema constants for Dolt MySQL table creation.
/// Three-layer design: canonical (query-optimized), raw (re-parseable), provenance (lineage).
/// See ADR-2005 for rationale and diagrams.
/// </summary>
public static class Schema
{
    // --- Layer 1: Canonical Tables ---

    public const string CreatePatients = """
        CREATE TABLE IF NOT EXISTS patients (
            id VARCHAR(255) PRIMARY KEY,
            family_name VARCHAR(255),
            given_name VARCHAR(255),
            birth_date DATE,
            gender VARCHAR(50),
            source_standard VARCHAR(50) DEFAULT 'fhir-r4',
            source_version VARCHAR(50) DEFAULT 'R4/4.0.1',
            extensions JSON,
            ingested_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
        )
        """;

    public const string CreateHealthRecords = """
        CREATE TABLE IF NOT EXISTS health_records (
            id VARCHAR(255) PRIMARY KEY,
            patient_id VARCHAR(255),
            record_type VARCHAR(100) NOT NULL,
            code VARCHAR(255),
            code_system VARCHAR(255),
            display VARCHAR(500),
            value_numeric DECIMAL(18,4),
            value_text TEXT,
            unit VARCHAR(100),
            device_name VARCHAR(255),
            device_type VARCHAR(100),
            effective_date DATETIME,
            source_standard VARCHAR(50) NOT NULL,
            source_version VARCHAR(50),
            extensions JSON,
            ingested_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
            CONSTRAINT fk_hr_patient FOREIGN KEY (patient_id) REFERENCES patients(id)
        )
        """;

    public const string CreateClinicalEntities = """
        CREATE TABLE IF NOT EXISTS clinical_entities (
            id VARCHAR(255) PRIMARY KEY,
            patient_id VARCHAR(255),
            entity_type VARCHAR(100) NOT NULL,
            code VARCHAR(255),
            code_system VARCHAR(255),
            display VARCHAR(500),
            confidence DECIMAL(5,4),
            needs_review BOOLEAN DEFAULT FALSE,
            source_text_span TEXT,
            model_id VARCHAR(255),
            source_standard VARCHAR(50),
            extensions JSON,
            ingested_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
            CONSTRAINT fk_ce_patient FOREIGN KEY (patient_id) REFERENCES patients(id)
        )
        """;

    // --- Layer 2: Raw Payload Archive ---

    public const string CreateRawPayloads = """
        CREATE TABLE IF NOT EXISTS raw_payloads (
            id VARCHAR(255) PRIMARY KEY,
            content_type VARCHAR(100) NOT NULL,
            source_standard VARCHAR(50) NOT NULL,
            source_version VARCHAR(50),
            payload LONGTEXT NOT NULL,
            payload_hash VARCHAR(64) NOT NULL,
            ingested_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
            UNIQUE KEY uq_payload_hash (payload_hash)
        )
        """;

    // --- Layer 3: Provenance Chain ---

    public const string CreateProvenance = """
        CREATE TABLE IF NOT EXISTS provenance (
            id VARCHAR(255) PRIMARY KEY,
            target_table VARCHAR(100) NOT NULL,
            target_id VARCHAR(255) NOT NULL,
            raw_payload_id VARCHAR(255) NOT NULL,
            transform VARCHAR(100) NOT NULL,
            transform_version VARCHAR(50),
            dolt_commit VARCHAR(64),
            created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
            CONSTRAINT fk_prov_raw FOREIGN KEY (raw_payload_id) REFERENCES raw_payloads(id)
        )
        """;

    /// <summary>All tables in dependency order (foreign keys satisfied by creation order).</summary>
    public static readonly string[] AllTables =
    [
        CreatePatients,
        CreateHealthRecords,
        CreateClinicalEntities,
        CreateRawPayloads,
        CreateProvenance,
    ];
}

/// <summary>
/// Utility for generating raw payload hashes (SHA-256) for deduplication.
/// </summary>
public static class PayloadHash
{
    public static string Compute(string payload)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexStringLower(bytes);
    }
}
