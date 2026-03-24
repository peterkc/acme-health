# ADR-2005: Multi-Standard Schema with Raw Payload Archive

## Status

Accepted

## Date

2026-03-24

## Context

The platform ingests health data from sources that use different standards, versions, and formats: FHIR R4 Bundles (EHR systems), CSV files (CGM devices), and planned Apple Health XML exports (wearables). Future sources include HL7v2 feeds (legacy systems), FHIR R5/R6, and TEFCA exchanges.

The current schema ([ADR-2002](2002-fhir-canonical-data-model.md)) uses flat, per-type tables (`patients`, `observations`, `cgm_readings`) with columns hardcoded to FHIR R4 field names. This creates five problems:

1. **No source provenance** -- a record could come from FHIR, HL7v2, or manual entry, with no way to distinguish
2. **No extension mechanism** -- ADR-2002 promises typed extensions but the schema has no column for them
3. **One table per data type** -- adding heart rate or sleep data requires a new table and new DDL
4. **No raw payload preservation** -- the original FHIR JSON or CSV is discarded after parsing, making re-processing impossible when standards change
5. **No FHIR version tracking** -- R4 and R5 records cannot coexist with version awareness

Standards evolve. FHIR R4 went normative in 2019; R5 published in 2023; R6 is in ballot. HL7v2, still used by 95% of US healthcare organizations, will coexist with FHIR for years. A health data platform must handle this evolution without losing data or requiring destructive schema migrations.

## Decision

Adopt a three-layer schema that separates what we know (canonical), what we received (raw), and where it came from (provenance).

### Layer 1: Canonical Tables (Query-Optimized)

Flat, indexed tables that services query against. Extended with `source_standard`, `code_system`, and a `JSON` extensions column.

- **patients** -- core identity, stable across all standards
- **health_records** -- unified measurements table replacing per-type tables (`observations`, `cgm_readings`). Differentiated by `record_type` and `code_system` columns
- **clinical_entities** -- AI extraction results with confidence scores and review flags

### Layer 2: Raw Payload Archive (Re-Parseable)

A single `raw_payloads` table stores the original JSON, XML, CSV, or HL7v2 message as received. Content-addressed via SHA-256 hash for deduplication.

When a standard changes (e.g., FHIR R5 renames a field), the raw payload can be re-parsed with an updated parser without data loss.

### Layer 3: Provenance Chain

A `provenance` table links each canonical record to its raw payload, recording which transform (parser + version) produced it and which Dolt commit captured the change.

## Considered Options

### 1. One table per standard

Separate tables for FHIR data, HL7v2 data, Apple Health data.

**Rejected**: table count grows with each new standard. Cross-standard queries (all heart rate readings regardless of source) require UNION across an unbounded number of tables. Breaks the "one canonical patient" principle from ADR-2002.

### 2. Full FHIR resource storage as JSON blobs

Store the complete FHIR JSON in a single column, query via JSON_EXTRACT.

**Rejected**: loses SQL queryability for the common case. `SELECT * FROM health_records WHERE code = '8302-2'` becomes `SELECT * FROM records WHERE JSON_EXTRACT(payload, '$.code.coding[0].code') = '8302-2'` -- slower, unindexable, and breaks for non-FHIR sources.

### 3. Canonical + Raw + Provenance (chosen)

Queryable canonical tables for the common read path. Raw archive for re-processing. Provenance for lineage. Best of both: fast queries AND lossless preservation.

## Architecture

### Data Flow

```
Source Data                  Parsers                    Database
-----------                  -------                    --------

FHIR R4 Bundle ---+
                  |
HL7v2 Message ----+--> Parser Layer --+--> raw_payloads (original)
                  |    (per-standard) |
Apple Health XML -+                   +--> canonical tables (flattened)
                  |                   |    (patients, health_records,
CGM CSV ----------+                   |     clinical_entities)
                                      |
                                      +--> provenance (links raw -> canonical)
                                      |
                                      +--> DOLT_COMMIT (version snapshot)
```

### Schema Relationships

```
+-------------------+      +-------------------+
| raw_payloads      |      | provenance        |
|-------------------|      |-------------------|
| id (PK)           |<-----| raw_payload_id    |
| content_type      |      | target_table      |
| source_standard   |      | target_id --------+---> patients.id
| payload (original)|      | transform         |---> health_records.id
| payload_hash      |      | transform_version |---> clinical_entities.id
+-------------------+      | dolt_commit       |
                            +-------------------+

+-------------------+      +-------------------+      +---------------------+
| patients          |      | health_records    |      | clinical_entities   |
|-------------------|      |-------------------|      |---------------------|
| id (PK)           |<--+--| patient_id (FK)   |  +---| patient_id (FK)     |
| family_name       |   |  | record_type       |  |   | entity_type         |
| given_name        |   |  | code              |  |   | code                |
| birth_date        |   |  | code_system       |  |   | code_system         |
| gender            |   +--| device_name       |  |   | confidence          |
| source_standard   |      | source_standard   |  |   | needs_review        |
| extensions (JSON) |      | extensions (JSON)  |  |   | source_standard     |
+-------------------+      +-------------------+  |   | extensions (JSON)   |
                                                   |   +---------------------+
                                                   +--- patients.id
```

### Standards Evolution

```
Today (FHIR R4):
  raw_payloads[fhir-r4] --parser-v1--> health_records[source=fhir-r4]

Tomorrow (FHIR R5 changes Observation):
  raw_payloads[fhir-r4] --parser-v2--> health_records[source=fhir-r4]
                                        (re-parsed with updated mapping)
                                        |
                                        v
                                   dolt_diff shows before/after
```

## Rationale

### Unified health_records replaces per-type tables

Instead of `observations` + `cgm_readings` + `heart_rate` + `sleep` + `steps`, one table handles all measurement types. The `record_type` and `code_system` columns differentiate. This mirrors FHIR's own design: Observation is one resource type, differentiated by code.

Adding Apple Health heart rate data requires zero schema changes -- just new rows with `record_type='heart-rate'`, `code_system='apple-healthkit'`.

### Raw payloads enable re-processing

When FHIR R5 changes the Observation resource structure, the raw FHIR R4 Bundle is preserved in `raw_payloads`. Write a new parser, re-process the archived payloads, update canonical records. Dolt tracks the before/after via `dolt_diff`.

This is the same pattern behind Epic's Chronicles (raw) + Clarity (canonical) architecture: preserve the original, project queryable views.

### Extensions via JSON column

ADR-2002 promises typed extensions for wearable metrics and AI confidence scores. The `extensions JSON` column on every canonical table delivers this without schema migration. MySQL 8.0 (and Dolt) supports indexed JSON path queries.

### Provenance closes the lineage loop

Every canonical record links back to its raw payload and the transform that created it. Combined with Dolt's commit history, this provides full data lineage: source → parser → canonical → version.

## Consequences

- **Schema migration required**: existing `observations` and `cgm_readings` data moves to `health_records`. Both ingest services update their INSERT targets.
- **API response unchanged**: external endpoints return the same shape. The migration is internal.
- **Storage increase**: raw payloads duplicate data (canonical + raw). Acceptable for a health platform where data preservation outweighs storage cost.
- **Query patterns change**: code that queries `observations` or `cgm_readings` directly must update to `health_records` with a `record_type` filter.
- **New capability**: any future data source (HL7v2, Apple Health, Oura API) can be added without DDL changes to canonical tables.

## Links

- Supersedes: per-type table pattern from initial scaffold
- Extends: [ADR-2002](2002-fhir-canonical-data-model.md) (FHIR R4 with extensions)
- Implemented in: `src/Acme.Stack.Core/Models.cs` (DDL), `src/Acme.Stack.FhirIngest/Program.cs`, `src/services/wearable-normalizer/main.py`
- Lineage documentation: [data-lineage.md](../architecture/data-lineage.md)
