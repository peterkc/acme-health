# ADR-0005: Version-Controlled Clinical Data with DoltgreSQL

## Status

Accepted

## Context

Health data platforms require:
- **Audit trails** (HIPAA): Who changed what, when, and why
- **Data lineage**: Tracing a field value back through every transformation
- **Point-in-time queries**: What did this patient's record look like on a specific date
- **Safe testing**: Branch the data, run migrations or ML experiments, merge or discard

Traditional approaches implement these in application code: audit tables with triggers, event sourcing with replay, soft deletes with timestamp filtering. Each adds complexity that every service must implement correctly.

## Decision

Use DoltgreSQL as the primary database. DoltgreSQL is PostgreSQL wire-compatible with git-style version control built into the storage engine.

## Options Considered

### 1. PostgreSQL + application-level audit tables

Standard approach. Each table gets a companion `_audit` table populated by triggers or application code.

**Rejected because**: Audit logic is scattered across services and easy to bypass. A direct SQL update (migration script, admin fix) skips application-level audit code. The audit table schema must be maintained alongside the main schema. Point-in-time queries require complex temporal joins.

### 2. PostgreSQL + event sourcing

Every state change is an immutable event. Current state is computed by replaying events.

**Rejected because**: Event sourcing adds significant complexity for querying current state. CQRS projections need maintenance. The replay mechanism must handle schema evolution. For a startup-stage product, the complexity overhead is not justified.

### 3. DoltgreSQL (chosen)

PostgreSQL wire protocol compatibility means existing drivers (Npgsql for C#, psycopg for Python) connect unchanged. Version control is a database primitive:

```sql
-- Full commit history of every data mutation
SELECT * FROM dolt_log ORDER BY date DESC LIMIT 10;

-- Diff patient records between two points in time
SELECT * FROM dolt_diff('patients', 'HEAD~5', 'HEAD');

-- Branch data for testing a migration
SELECT dolt_branch('migration-test');
-- Run migration on branch, validate, then merge or drop

-- Merge validated changes back
SELECT dolt_merge('migration-test');
```

## Rationale

DoltgreSQL moves audit trails from application code to the storage engine. Specific benefits:

**HIPAA compliance**: `dolt_log` provides a complete, tamper-evident history of every data mutation. No application code can bypass it — the versioning happens at the storage layer.

**Point-in-time queries**: `AS OF` syntax queries any historical state without maintaining temporal tables or audit schemas.

**Safe data operations**: `dolt_branch` creates an isolated copy of the database (without copying data — it's structural sharing, like git). Run migrations, test ML model outputs, or validate data imports on a branch. Merge if correct, drop if not.

**Zero driver changes**: DoltgreSQL speaks PostgreSQL wire protocol. Npgsql (C#) and psycopg (Python) connect with a standard connection string. ORMs (EF Core, SQLAlchemy) work unchanged. The application code doesn't know it's talking to Dolt.

**Escape hatch**: If DoltgreSQL's performance or ecosystem maturity becomes a limitation at scale, migrating to plain PostgreSQL is a connection string change. The application code is standard SQL. The versioning features are additive — removing them means losing audit capabilities, not breaking functionality.

## Trade-offs

- DoltgreSQL is newer than PostgreSQL. The ecosystem (extensions, managed hosting, tooling) is smaller.
- Query performance for analytical workloads may differ from PostgreSQL. Benchmarking needed for production use.
- The team needs to learn Dolt-specific operations (`dolt_commit`, `dolt_branch`, `dolt_merge`) for data management workflows. Standard SQL queries work identically.

## Consequences

- All services connect to DoltgreSQL via standard PostgreSQL drivers. No Dolt-specific client libraries needed.
- Data management workflows (migrations, imports, corrections) use branches instead of backup/restore cycles.
- The Aspire AppHost runs DoltgreSQL as a container (`dolthub/doltgresql`).
- CI/CD can branch the database per test run — true isolation without database cloning overhead.
