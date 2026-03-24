# Threat Model

> STRIDE analysis applied to the ACME Health service boundaries.
> Demo project using Synthea synthetic data — threats are analyzed against the architecture, not a production deployment.

## Scope

Four services, two data stores, one external API:

- **FHIR Ingest** (C#) — accepts FHIR R4 Bundles, persists Patient and Observation resources
- **Data API** (C#) — serves patient data (stub)
- **Wearable Normalizer** (Python) — ingests CGM CSV files
- **Clinical Extractor** (Python) — extracts entities from clinical notes via Anthropic API (stub)
- **Dolt MySQL** — versioned clinical data store
- **Redis** — cache layer (no patient data)
- **Anthropic API** — external LLM service for entity extraction

Service topology defined in [`AppHost.cs`](../../src/AppHost/AppHost.cs).

## Trust Boundaries

Three trust boundaries exist in the architecture:

```
                    TB-1                        TB-2                       TB-3
  External ─────────|──── Aspire Services ──────|──── Dolt MySQL    Services ──|── Anthropic API
  Clients           |    (FHIR Ingest,          |    (acme_health)            |   (external)
                    |     Wearable Normalizer,  |    Password auth            |   API key auth
                    |     Data API,             |                             |
                    |     Clinical Extractor)   |                             |
```

**TB-1**: External clients to services. No authentication in current implementation.

**TB-2**: Services to Dolt MySQL. Password-authenticated via Aspire secrets ([`AppHost.cs:5-6`](../../src/AppHost/AppHost.cs#L5)). MySQL wire protocol.

**TB-3**: Clinical Extractor to Anthropic API. API key authentication. Clinical note text crosses this boundary.

## STRIDE Analysis

### FHIR Ingest

| Threat | Category | Current Mitigation | Risk Level |
|--------|----------|-------------------|------------|
| Unauthenticated clients submit arbitrary bundles | Spoofing | None — no authentication | High (planned: SMART on FHIR) |
| Malformed FHIR JSON causes deserialization errors | Tampering | Firely SDK validates FHIR structure; invalid JSON returns HTTP 422 ([`Program.cs:65-71`](../../src/Acme.Stack.FhirIngest/Program.cs#L65)) | Low |
| SQL injection via patient/observation fields | Tampering | Parameterized queries with `@` placeholders ([`Program.cs:135-141`](../../src/Acme.Stack.FhirIngest/Program.cs#L135)) | Low |
| Data changes without attribution | Repudiation | Dolt commit log tracks every mutation ([`Program.cs:183`](../../src/Acme.Stack.FhirIngest/Program.cs#L183)) | Low |
| Patient data exposed via API responses | Info Disclosure | Response returns counts and commit hash, not patient data ([`Program.cs:196-201`](../../src/Acme.Stack.FhirIngest/Program.cs#L196)) | Low |
| Large bundles exhaust memory | DoS | No request size limit configured | Medium (planned: rate limiting) |
| No role separation between read and write | Elevation | Single database user for all operations | Medium (planned: RBAC) |

### Wearable Normalizer

| Threat | Category | Current Mitigation | Risk Level |
|--------|----------|-------------------|------------|
| Unauthenticated CSV uploads | Spoofing | None — no authentication | High |
| Oversized CSV exhausts memory | DoS | 10 MB limit enforced at chunk read ([`main.py:215-219`](../../src/services/wearable-normalizer/main.py#L215)) | Low |
| SQL injection via CSV field values | Tampering | Parameterized queries with `%s` placeholders ([`main.py:256-261`](../../src/services/wearable-normalizer/main.py#L256)) | Low |
| Malformed CSV rows cause processing errors | Tampering | Malformed rows dropped with warning count ([`main.py:178-180`](../../src/services/wearable-normalizer/main.py#L178)) | Low |

### Clinical Extractor (Stub)

| Threat | Category | Current Mitigation | Risk Level |
|--------|----------|-------------------|------------|
| Prompt injection via clinical note text | Tampering | Not yet implemented — [ADR-2004](../adr/2004-human-in-the-loop-clinical-ai.md) mandates HITL review before any extraction commits | Deferred |
| Hallucinated clinical data (medications, codes) | Integrity | Confidence scoring + human review queue (ADR-2004) | Designed, not implemented |
| API key exposure | Info Disclosure | Aspire injects secrets via environment variables | Low |
| Clinical text sent to external API | Info Disclosure | Anthropic API does not persist request data per their data policy; production would need BAA | Medium |

### Dolt MySQL

| Threat | Category | Current Mitigation | Risk Level |
|--------|----------|-------------------|------------|
| Unauthorized database access | Spoofing | Password authentication via `DOLT_ROOT_PASSWORD` ([`AppHost.cs:12`](../../src/AppHost/AppHost.cs#L12)) | Medium |
| Audit trail tampering | Tampering | Dolt commits are content-addressed (hash chain) — altering history changes downstream hashes | Low |
| Data at rest unencrypted | Info Disclosure | Docker volume (`dolt-data`) is not encrypted | Medium (infrastructure concern) |

## Data Classification

| Data Type | PHI Status | Location | Source |
|-----------|-----------|----------|--------|
| Patient demographics (name, DOB, gender) | Synthetic PHI | `patients` table in Dolt MySQL | Synthea FHIR Bundles |
| Clinical observations (codes, values, dates) | Synthetic PHI | `observations` table in Dolt MySQL | Synthea FHIR Bundles |
| CGM glucose readings | Not PHI | `cgm_readings` table in Dolt MySQL | GlucoBench public dataset (no identifiers) |
| Clinical note text | Potentially PHI | Transient (request memory only) | MTSamples (CC0, de-identified) |
| Extracted entities (medications, ICD-10 codes) | Derived from PHI | Not yet persisted | Clinical-extractor output (stub) |

## Mitigations In Place

1. **Parameterized queries** — SQL injection prevented in both C# (`@param`) and Python (`%s` with `executemany`)
2. **FHIR validation** — Firely SDK enforces R4 structure; malformed bundles rejected at HTTP 422
3. **Input size limits** — CSV upload capped at 10 MB; chunked reading prevents memory exhaustion
4. **Dolt audit trail** — storage-layer version control that application code cannot bypass
5. **Secrets management** — Database password injected via Aspire secrets, not hardcoded
6. **HITL architecture** — AI extractions require confidence threshold or human sign-off before committing to patient record (ADR-2004)

## Mitigations Planned

1. **SMART on FHIR** — OAuth 2.0 + PKCE authentication for all API endpoints
2. **FHIR Consent** — Granular authorization based on patient consent records (see [consent-architecture.md](../ai-governance/consent-architecture.md))
3. **Rate limiting** — Per-client request throttling on ingestion endpoints
4. **TLS enforcement** — HTTPS required for all service communication (Aspire configures this; development uses self-signed certificates)
5. **Per-service database credentials** — Replace single root user with service-specific accounts for audit attribution
6. **Prompt injection defense** — Input sanitization and output validation for clinical-extractor

## Residual Risks

- **No authentication** on any endpoint. Anyone with network access can submit data or query patients. This is acceptable for a demo project with synthetic data; production requires SMART on FHIR at minimum.
- **Single database user** makes it impossible to distinguish which service made a change from the database perspective alone. The Dolt commit message includes the source service, but this is application-level attribution, not database-level identity.
- **Clinical text crosses TB-3** to the Anthropic API. In production, this requires a BAA with the API provider and patient consent for AI processing of their clinical notes.
