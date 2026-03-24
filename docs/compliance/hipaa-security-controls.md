# HIPAA Security Controls Mapping

> This is a demo project using Synthea synthetic data. No real PHI is processed and no covered entity obligations apply.
> This document maps HIPAA Security Rule safeguards to architecture decisions to demonstrate how the platform would satisfy these requirements in a production deployment.

## Purpose

The HIPAA Security Rule (45 CFR Part 164, Subpart C) requires administrative, physical, and technical safeguards for electronic protected health information (ePHI). Rather than describing what HIPAA requires in the abstract, this document maps each relevant safeguard to a specific ACME Health architecture decision, code path, or design document.

## Administrative Safeguards (§164.308)

### Risk Analysis — §164.308(a)(1)(ii)(A)

The risk analysis for this platform is documented in [threat-model.md](../architecture/threat-model.md). It identifies trust boundaries, applies STRIDE analysis to each service, classifies data by PHI status, and lists mitigations (both implemented and planned).

Missing or inadequate risk assessments account for approximately 68% of HIPAA civil monetary penalty settlements. The threat model is the artifact that satisfies this requirement.

### Information System Activity Review — §164.308(a)(1)(ii)(D)

Dolt MySQL provides activity review at the storage layer. The `dolt_log` system table records every committed change with committer identity, timestamp, and message. The `dolt_diff()` function shows row-level before/after values between any two commits.

This operates below the application layer — a direct SQL modification that bypasses the API still appears in the commit history. See [data-lineage.md](../architecture/data-lineage.md) for query examples.

### Access Authorization — §164.308(a)(4)

Current state: single database user (`root`) for all services. This satisfies the letter of "unique user identification" at the service level but not the spirit of per-user attribution.

Planned: per-service database credentials managed through Aspire secrets, enabling audit attribution to the service that made each change. The Dolt commit message currently includes the source service name as a workaround ([`Program.cs:184`](../../src/Acme.Stack.FhirIngest/Program.cs#L184)).

### Security Incident Procedures — §164.308(a)(6)

The Dolt commit history serves as a forensic record. If a security incident is suspected, the investigation process is:

```sql
-- 1. Identify recent changes to the affected table
SELECT * FROM dolt_diff('patients', 'HEAD~50', 'HEAD')
WHERE to_id = 'affected-patient-id';

-- 2. Check commit context
SELECT commit_hash, committer, date, message
FROM dolt_log
WHERE date > '2026-03-01';

-- 3. Time-travel to the pre-incident state
CALL DOLT_CHECKOUT('pre-incident-commit-hash');
```

No formal incident response plan exists for this demo project. Production would require a written plan with designated response team, breach determination process, and notification procedures per the Breach Notification Rule (45 CFR §§164.400-414).

## Technical Safeguards (§164.312)

### Access Control — §164.312(a)

| Requirement | Implementation |
|-------------|----------------|
| Unique user identification | Database password authentication via Aspire secrets ([`AppHost.cs:5-6`](../../src/AppHost/AppHost.cs#L5)) |
| Emergency access procedure | Not implemented (demo scope) |
| Automatic logoff | Not applicable — services are stateless REST APIs with no user sessions |
| Encryption | Planned: TLS for transport. Dolt data volume is not encrypted at rest (infrastructure concern) |

API endpoint authentication (SMART on FHIR) is planned but not implemented. Current state: unauthenticated access. See [threat-model.md](../architecture/threat-model.md) for the risk assessment.

### Audit Controls — §164.312(b)

This is the platform's strongest HIPAA alignment. Dolt MySQL provides audit controls without application-level logging code:

| Audit Requirement | Dolt Feature |
|-------------------|--------------|
| Record who changed data | `dolt_log.committer` |
| Record what changed | `dolt_diff()` — row-level before/after |
| Record when | `dolt_log.date` |
| Record context | `dolt_log.message` (includes source service and timestamp) |
| Prevent tampering | Content-addressed hash chain |
| Retain for 6 years | Full history retained by default |

See [data-lineage.md](../architecture/data-lineage.md) for the complete audit trail architecture.

### Integrity — §164.312(c)

Two mechanisms protect ePHI integrity:

1. **Input validation**: The Firely SDK deserializes FHIR Bundles with compile-time type safety ([`Program.cs:61`](../../src/Acme.Stack.FhirIngest/Program.cs#L61)). A `Patient` resource with a string where a date is expected fails at deserialization, not at database insert. This prevents a class of data corruption that string-based JSON parsing misses.

2. **Immutable history**: Dolt commits are content-addressed. Modifying a committed row changes its hash, which changes the commit hash, which changes all downstream hashes. Tampering with historical data is detectable by hash verification.

### Transmission Security — §164.312(e)

Aspire configures HTTPS for service communication. Development uses self-signed certificates. Production deployment would enforce TLS 1.2+ for all service-to-service and client-to-service communication.

Database connections use MySQL wire protocol (not encrypted by default). Production would require TLS-enabled MySQL connections via the `SslMode=Required` connection string parameter.

## Physical Safeguards (§164.310)

Physical safeguards (facility access controls, workstation security, device and media controls) are infrastructure concerns addressed at the deployment layer, not the application layer.

For this demo project running locally via Docker containers, physical security is the developer's workstation. A production deployment on cloud infrastructure (AWS, Azure, GCP) would inherit physical safeguards from the cloud provider's SOC 2 / ISO 27001 certifications.

## Architecture Advantages

The choice of Dolt MySQL for the data layer ([ADR-1001](../adr/1001-doltgresql-versioned-clinical-data.md)) provides three properties that application-level audit solutions cannot match:

1. **Cannot be bypassed**: A trigger-based audit trail can be disabled by anyone with `ALTER TABLE` privileges. Dolt's version history is integral to the storage engine.

2. **Row-level granularity**: `dolt_diff()` shows exactly which fields changed, from what value to what value. Application-level logging typically captures "a change was made" without before/after values.

3. **Time-travel queries**: `DOLT_CHECKOUT` allows querying the database as it existed at any prior commit. This supports incident investigation ("what did the data look like before this change?") without maintaining separate snapshot tables.

## Gaps

| Safeguard | Gap | Severity |
|-----------|-----|----------|
| §164.308(a)(5) — Workforce training | Not applicable (solo developer, demo project) | N/A |
| §164.312(a)(1) — Authentication | No endpoint authentication | High for production |
| §164.312(a)(2)(iv) — Encryption at rest | Docker volume not encrypted | Medium |
| §164.312(e)(1) — Transmission security | MySQL connections not TLS-encrypted in dev | Medium |
| §164.308(a)(6) — Incident response plan | No formal plan | Medium for production |

These gaps are documented, not hidden. Each has a planned mitigation path described in [threat-model.md](../architecture/threat-model.md).
