# Architecture

> Demo project using [Synthea](https://synthea.mitre.org/) synthetic data. No real PHI is processed.
> Architecture decisions demonstrate production patterns; operational maturity is not claimed.

## System Overview

ACME Health is a polyglot health data platform. .NET Aspire orchestrates four services — two in C#, two in Python — against a shared Dolt MySQL database. One command (`dotnet run --project src/AppHost`) starts everything.

The stack splits by strength: C# with the Firely SDK handles FHIR R4 parsing where type safety prevents wrong medication doses from bad field mappings. Python handles wearable data normalization (pandas) and clinical entity extraction (Anthropic SDK) where the ML ecosystem is strongest.

See [README.md](README.md) for setup instructions and technology rationale.

## Services

| Service | Language | Port | Endpoint | Status |
|---------|----------|------|----------|--------|
| fhir-ingest | C# (.NET 10) | 5000 | `POST /fhir/Bundle` | Active |
| data-api | C# (.NET 10) | 5001 | `GET /patients` | Stub |
| wearable-normalizer | Python (FastAPI) | 8001 | `POST /ingest/cgm` | Active |
| clinical-extractor | Python (FastAPI) | 8002 | `POST /extract`, `POST /review` | Stub |

Defined in [`src/AppHost/AppHost.cs`](src/AppHost/AppHost.cs). Aspire injects connection strings, propagates health checks, and configures distributed tracing (OpenTelemetry) across all services.

## Data Boundaries

PHI (synthetic, from Synthea) exists in one place: the Dolt MySQL database (`acme_health`).

| Component | Stores PHI | Rationale |
|-----------|-----------|-----------|
| Dolt MySQL | Yes | Patient demographics, observations, CGM readings |
| Redis | No | Cache layer — no patient data stored |
| Anthropic API | Transient | Clinical notes sent for extraction; not persisted by the API |
| Service memory | Transient | Request-scoped only; no in-memory patient caches |

This boundary is enforced by architecture, not policy. Services connect to Dolt for persistence and Redis for caching. No service stores patient data in local files or secondary databases.

## Audit Trail

Dolt MySQL provides version control at the storage layer. Every `INSERT` or `UPDATE` can be followed by `CALL DOLT_COMMIT(...)`, creating an immutable commit history without application-level logging code.

```sql
-- What changed, when, and in what context
SELECT * FROM dolt_log;

-- Row-level diff between any two commits
SELECT * FROM dolt_diff('patients', 'HEAD~5', 'HEAD');

-- Time-travel query against a prior state
CALL DOLT_CHECKOUT('main~1');
SELECT * FROM patients;
```

Both active services call `DOLT_COMMIT` after each ingest operation:
- FHIR Ingest: [`Program.cs:183`](src/Acme.Stack.FhirIngest/Program.cs#L183) — `CALL DOLT_COMMIT('-Am', @msg)`
- Wearable Normalizer: [`main.py:271`](src/services/wearable-normalizer/main.py#L271) — `CALL DOLT_COMMIT('-Am', %s)`

DOLT_COMMIT is best-effort in both services — data persists even if the commit call fails. See [data-lineage.md](docs/architecture/data-lineage.md) for the full lineage story.

## Security Posture

### Implemented

- **Parameterized queries** in both C# ([`Program.cs:135-141`](src/Acme.Stack.FhirIngest/Program.cs#L135)) and Python ([`main.py:256-261`](src/services/wearable-normalizer/main.py#L256)) — no string concatenation for SQL
- **FHIR validation** via Firely SDK ([`Program.cs:61`](src/Acme.Stack.FhirIngest/Program.cs#L61)) — compile-time type safety on nested FHIR resource types
- **Database authentication** via Aspire secrets ([`AppHost.cs:5-6`](src/AppHost/AppHost.cs#L5))
- **Dolt audit trail** — storage-layer commit history that application code cannot bypass
- **Input validation** — FHIR Bundle structure enforcement (HTTP 422), CSV size limits (10 MB), malformed row handling

### Planned

- **SMART on FHIR** authentication for EHR API access (OAuth 2.0 + PKCE)
- **FHIR Consent** resource enforcement before data access — see [consent-architecture.md](docs/ai-governance/consent-architecture.md)
- **Rate limiting** on ingestion endpoints
- **TLS enforcement** (Aspire configures HTTPS; development uses self-signed certificates)

See [threat-model.md](docs/architecture/threat-model.md) for the full STRIDE analysis.

## AI Governance

The clinical-extractor uses LLMs to extract structured entities from unstructured clinical notes. [ADR-2004](docs/adr/2004-human-in-the-loop-clinical-ai.md) mandates that no AI extraction commits to the patient record without either high confidence (>0.85) or human sign-off.

| Document | What It Covers |
|----------|----------------|
| [Model Card](docs/ai-governance/model-card.md) | Intended use, limitations, evaluation plan, FDA regulatory position |
| [AI RMF Alignment](docs/ai-governance/ai-rmf.md) | NIST AI Risk Management Framework mapped to project decisions |
| [Bias Audit Framework](docs/ai-governance/bias-audit.md) | Demographic stratification using Synthea data, DPD/EOR metrics |
| [Consent Architecture](docs/ai-governance/consent-architecture.md) | FHIR Consent enforcement design (planned) |
| [SDOH Rationale](docs/ai-governance/sdoh-rationale.md) | Social determinants feature inclusion/exclusion decisions |

## Compliance

| Document | What It Covers |
|----------|----------------|
| [HIPAA Security Controls](docs/compliance/hipaa-security-controls.md) | Security Rule safeguards mapped to architecture |
| [Data Lineage](docs/architecture/data-lineage.md) | Mutation tracking from source to versioned storage |
| [Threat Model](docs/architecture/threat-model.md) | STRIDE analysis per service boundary |

## Architecture Decision Records

| ADR | Decision | Status |
|-----|----------|--------|
| [1001](docs/adr/1001-doltgresql-versioned-clinical-data.md) | Dolt MySQL for versioned clinical data | Superseded (DoltgreSQL → Dolt MySQL) |
| [2001](docs/adr/2001-polyglot-aspire-orchestration.md) | C# + Python polyglot with Aspire orchestration | Accepted |
| [2002](docs/adr/2002-fhir-canonical-data-model.md) | FHIR R4 as canonical data model with typed extensions | Accepted |
| [2003](docs/adr/2003-monorepo-project-structure.md) | Monorepo with Aspire service composition | Accepted |
| [2004](docs/adr/2004-human-in-the-loop-clinical-ai.md) | Human-in-the-loop for clinical AI extractions | Accepted |

## What This Is Not

This is a research project exploring healthcare platform architecture. It processes synthetic patient data generated by [Synthea](https://synthea.mitre.org/) and public clinical text from [MTSamples](https://www.kaggle.com/datasets/tboyle10/medicaltranscriptions).

- No real patient data is stored or processed
- No HIPAA covered entity obligations apply
- The clinical-extractor is a stub — the governance documentation describes the designed architecture, not a deployed system
- Security certifications (SOC 2, HITRUST) are referenced for context, not claimed
