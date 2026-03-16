# ADR-2003: Monorepo with Aspire Composition

## Status

Accepted

## Date

2026-03-16

## Context

The platform consists of 4 services (FHIR ingestion, data API, wearable normalizer, clinical extractor), a shared domain model, a database, and supporting tools. These components need to share data contracts, deploy together during development, and present a coherent system to evaluators reviewing the repository.

## Decision

Single monorepo with `src/` for C# projects, `src/services/` for Python services, `tools/` for CLI utilities, and `docs/` for architecture documentation and ADRs. Aspire's AppHost composes the service topology in code.

## Considered Options

### 1. Separate repos per service

Each service in its own GitHub repository. Independent CI/CD, versioning, and deployment.

**Rejected because**: At a 3-person startup, the coordination overhead of multi-repo outweighs the isolation benefits. Schema changes to the shared patient data model require synchronized PRs across repos. Reviewers evaluating the codebase need to clone multiple repos to understand the system.

### 2. Monorepo with Aspire (chosen)

All services in one repo. `Acme.Stack.Core` provides shared domain models consumed by C# projects. Python services access the same data model via database tables and API contracts. The Aspire AppHost defines the service topology — which services exist, how they connect, what infrastructure they depend on.

### 3. Monorepo with Docker Compose only

Same repo structure but Docker Compose instead of Aspire for orchestration.

**Rejected because**: Docker Compose doesn't provide service discovery, health check propagation, or distributed tracing. The Aspire dashboard gives visual service topology and request tracing across C# and Python services with zero configuration.

## Rationale

The monorepo structure:

```
src/
  AppHost/                      # Service topology as code
  Acme.Stack.Core/              # Shared domain models
  Acme.Stack.FhirIngest/        # FHIR R4 parsing (C#)
  Acme.Stack.DataApi/           # Unified patient API (C#)
  services/
    wearable-normalizer/        # Python/FastAPI
    clinical-extractor/         # Python/FastAPI
tools/
  schema-migrate/               # CLI utility
docs/
  adr/                          # This file lives here
```

One `dotnet run --project src/AppHost` starts the entire platform. This matters for:

- **Developer onboarding**: New engineers run one command, see the Aspire dashboard, and understand the service topology
- **Code review**: Changes to the patient data model and the services consuming it are in the same PR
- **Architecture visibility**: The AppHost `Program.cs` is a readable declaration of the system — services, databases, dependencies, health checks

## Consequences

- All services version together. No independent release cadence per service.
- The repo grows larger over time. For a startup-stage product, this is acceptable.
- CI runs all builds and tests on every PR. Build caching (dotnet incremental build, Python uv cache) keeps this fast.
- The `docs/adr/` directory is part of the repo because architectural decisions are as much a part of the system as the code.

## Links

- Implemented in: repo root structure, `src/AppHost/Program.cs`
