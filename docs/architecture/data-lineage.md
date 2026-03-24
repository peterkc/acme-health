# Data Lineage

> Every data mutation in ACME Health is tracked from source to versioned storage.
> Synthea synthetic data only — no real PHI.

## Design Principle

Clinical data platforms typically implement audit trails in application code: triggers, event tables, change-data-capture pipelines. Each approach adds surface area that must be maintained and can be bypassed by direct database access.

Dolt MySQL takes a different approach. Version control lives in the storage engine. Every row change can be committed, diffed, and reverted using SQL — the same interface the application already uses. Application code cannot bypass the audit trail because the audit trail *is* the storage layer.

[ADR-1001](../adr/1001-doltgresql-versioned-clinical-data.md) documents this decision and the migration from DoltgreSQL to Dolt MySQL.

## Three-Layer Architecture

[ADR-2005](../adr/2005-multi-standard-schema.md) introduces a three-layer schema that separates query-optimized storage, raw payload archival, and provenance tracking.

**Layer 1 — Canonical tables** (`patients`, `health_records`, `clinical_entities`)
Flattened, query-optimized rows. Application queries, dashboards, and analytics operate on this layer. Records are normalized regardless of source format.

**Layer 2 — Raw payload archive** (`raw_payloads`)
Original source content preserved verbatim — JSON for FHIR, CSV text for wearable uploads. Enables re-processing when parsers are updated or bugs are fixed, without re-fetching from the origin system.

**Layer 3 — Provenance chain** (`provenance`)
Links each canonical record back to the raw payload that produced it, along with the transform version and Dolt commit hash. Answers "where did this row come from and how was it produced?"

```text
Source --> Parser --> raw_payloads (original preserved)
                 --> canonical tables (flattened for queries)
                 --> provenance (links raw -> canonical)
                 --> DOLT_COMMIT (version snapshot)
```

All three layers are written within a single transaction before the Dolt commit is called. If the transaction fails, none of the three layers are partially written.

## Lineage by Data Source

### FHIR R4 Bundles (EHR / Synthea)

```text
Synthea JSON → Firely SDK → raw_payloads + health_records + provenance → DOLT_COMMIT
```

1. Client submits a FHIR R4 Bundle to `POST /fhir/Bundle`
2. Firely SDK deserializes with compile-time type safety ([`Program.cs:61`](../../src/Acme.Stack.FhirIngest/Program.cs#L61))
3. `Patient` and `Observation` resources are mapped to flat records in the `patients` and `health_records` tables ([`Program.cs:208-261`](../../src/Acme.Stack.FhirIngest/Program.cs#L208))
4. Raw Bundle JSON is archived to `raw_payloads` with `source_standard = 'fhir-r4'`
5. A `provenance` record is created linking each `health_records` row to its `raw_payloads` entry with transform metadata
6. UPSERT into canonical tables plus raw archive within a transaction ([`Program.cs:117-175`](../../src/Acme.Stack.FhirIngest/Program.cs#L117))
7. `CALL DOLT_COMMIT('-Am', @msg)` records the change ([`Program.cs:183-184`](../../src/Acme.Stack.FhirIngest/Program.cs#L183))

The transaction commits before the Dolt commit. Data persists even if versioning fails — the commit is best-effort ([`Program.cs:192-193`](../../src/Acme.Stack.FhirIngest/Program.cs#L192)).

### CGM CSV (Wearable Devices)

```text
CSV upload → pandas normalization → raw_payloads + health_records + provenance → DOLT_COMMIT
```

1. Client uploads a CSV to `POST /ingest/cgm` (10 MB limit enforced at [`main.py:215-219`](../../src/services/wearable-normalizer/main.py#L215))
2. Column name variants are resolved case-insensitively ([`main.py:32-33`](../../src/services/wearable-normalizer/main.py#L32))
3. Timestamps are normalized to UTC ISO 8601; malformed rows are dropped with a warning count ([`main.py:167-191`](../../src/services/wearable-normalizer/main.py#L167))
4. Normalized readings are written to the `health_records` table via batch insert ([`main.py:256-261`](../../src/services/wearable-normalizer/main.py#L256))
5. Original CSV content is archived to `raw_payloads` with `source_standard = 'csv/cgm'`
6. A `provenance` record is created linking each `health_records` row to its `raw_payloads` entry
7. `CALL DOLT_COMMIT('-Am', %s)` records the change ([`main.py:271`](../../src/services/wearable-normalizer/main.py#L271))

Same best-effort pattern: data persists, versioning is logged but not request-fatal ([`main.py:277-280`](../../src/services/wearable-normalizer/main.py#L277)).

### Clinical Notes (AI Extraction)

```
Clinical text → Anthropic API → extraction with confidence → HITL review queue → patient record
```

This path is designed but not yet implemented. The clinical-extractor endpoints (`POST /extract`, `POST /review`) return `{"status": "not_implemented"}` ([`main.py:18`](../../src/services/clinical-extractor/main.py#L18)).

[ADR-2004](../adr/2004-human-in-the-loop-clinical-ai.md) defines the full architecture:

- Every extracted entity carries a confidence score (0.0-1.0)
- Fields below threshold (default 0.85) enter a human review queue
- The extraction record stores provenance: source document, text span, model, timestamp
- Contradiction detection runs before any commit to the patient record

When implemented, this path will follow the same Dolt commit pattern for audit trail consistency.

## Querying the Audit Trail

These queries work against the running Dolt MySQL instance. Connect via any MySQL client.

```sql
-- Commit history: who changed what, when, with what message
SELECT commit_hash, committer, date, message
FROM dolt_log
ORDER BY date DESC
LIMIT 20;

-- Row-level diff between two commits
-- Shows exactly which patient rows changed and how
SELECT *
FROM dolt_diff('patients', 'HEAD~5', 'HEAD');

-- Time-travel: query the database as it existed at a prior commit
CALL DOLT_CHECKOUT('main~1');
SELECT * FROM patients WHERE id = 'abc-123';
CALL DOLT_CHECKOUT('main');  -- return to HEAD

-- Find when a specific patient record was last modified
SELECT commit_hash, date, message
FROM dolt_log
WHERE commit_hash IN (
    SELECT to_commit
    FROM dolt_diff('patients', 'HEAD~100', 'HEAD')
    WHERE to_id = 'patient-uuid-here'
);
```

## HIPAA Audit Controls Mapping

HIPAA Security Rule §164.312(b) requires mechanisms to "record and examine activity in information systems that contain or use electronic protected health information."

| HIPAA Requirement | Dolt Implementation |
|-------------------|---------------------|
| Record who changed data | `dolt_log.committer` (database-level identity) |
| Record what changed | `dolt_diff()` — row-level before/after values |
| Record when it changed | `dolt_log.date` — commit timestamp |
| Record the context | `dolt_log.message` — commit message from application (e.g., "Ingest: FHIR Bundle at 2026-03-21T14:30:00Z") |
| Prevent log tampering | Dolt commits are content-addressed (hash chain) — modifying history changes all downstream hashes |
| Retention | Dolt retains full history by default — no separate retention configuration needed for audit compliance |

The audit trail cannot be bypassed by application code. A direct SQL `INSERT` without a subsequent `DOLT_COMMIT` still shows up in the working set diff (`dolt_status`), making uncommitted changes visible.

## Provenance Queries

These queries use the three-layer schema to trace canonical records back to their source.

```sql
-- Full lineage for a health record
SELECT hr.id, hr.record_type, hr.code, p.transform, p.dolt_commit, rp.source_standard
FROM health_records hr
JOIN provenance p ON p.target_id = hr.id AND p.target_table = 'health_records'
JOIN raw_payloads rp ON rp.id = p.raw_payload_id
WHERE hr.patient_id = 'patient-uuid-here';

-- Re-process: find all raw payloads from a specific standard
SELECT id, content_type, source_version, ingested_at
FROM raw_payloads
WHERE source_standard = 'fhir-r4';
```

## Limitations

1. **Best-effort versioning**: Both services treat `DOLT_COMMIT` as non-fatal. If the commit fails, data persists but the audit trail has a gap. Production would need alerting on failed commits.

2. **Single committer identity**: Current implementation uses the Dolt root user for all commits. Production would need per-service or per-user database credentials to distinguish who made changes.

3. **No retention management**: Dolt retains all history by default. At production scale, this creates storage growth. A retention policy (e.g., `dolt gc` with shallow history) would be needed, balanced against audit compliance periods (HIPAA requires 6-year retention for compliance documents).

4. **Commit granularity**: Each ingest request creates one commit. A FHIR Bundle with 1,000 observations creates a single commit, not per-observation commits. The audit trail shows "this bundle was ingested" but not "this specific observation was added" — the `dolt_diff` query fills that gap at query time.

5. **Raw payload storage growth**: The raw_payloads table stores original payloads alongside canonical records. At scale, archival policies (compress after N days, move to cold storage) would be needed.
