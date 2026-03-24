# ADR-2001: Polyglot Architecture with .NET Aspire Orchestration

## Status

Accepted

## Date

2026-03-16

## Context

A health data platform ingests from multiple source types (FHIR APIs, wearable devices, clinical PDFs), normalizes into a canonical model, serves unified APIs, and applies AI for clinical entity extraction. Each component has different optimal language characteristics:

- **FHIR parsing**: Complex nested resource model (Patient, Observation, MedicationRequest) where a wrong field type can mean a wrong medication dose. Type safety matters at the data-model level.
- **AI/ML extraction**: LLM SDKs (Anthropic, OpenAI), NLP libraries (spaCy), and data manipulation (pandas) are Python-native. The ecosystem gap in .NET is real.
- **Wearable normalization**: Health data libraries (Apple HealthKit parsers, Fitbit SDKs) ship Python-first.

A greenfield health platform with Python/FastAPI as the initial stack. The question: stay pure Python or adopt a polyglot approach where it pays off?

## Decision

Use .NET Aspire as the orchestration layer, with C# for FHIR ingestion and data API services, and Python/FastAPI for wearable normalization and clinical NLP extraction.

## Considered Options

### 1. Pure Python (FastAPI + Docker Compose)

Matches the existing stack. Single language for all services. Docker Compose for orchestration.

**Rejected because**: No compile-time type safety on FHIR resources. A `CodeableConcept` arriving as a string is a runtime error discovered in production, not a build error discovered in CI. Docker Compose provides no built-in service discovery, health checks, or distributed tracing.

### 2. Pure C# (.NET Aspire)

Strong typing everywhere. Aspire provides orchestration, observability, and service discovery out of the box.

**Rejected because**: The AI/ML ecosystem in .NET is immature compared to Python. Porting LLM extraction pipelines to C# means fewer library options, less community support, and slower iteration on the AI layer.

### 3. Polyglot with Aspire (chosen)

C# where type safety is a clinical safety mechanism. Python where the ecosystem is strongest. Aspire orchestrates both with `AddProject<>()` for C# and `AddUvicornApp()` for Python/FastAPI.

## Rationale

Type safety on clinical data is a patient safety mechanism, not a language preference. The Firely SDK (`Hl7.Fhir.R4`) provides strongly-typed FHIR R4 resources — the compiler enforces the data model. When parsing an Epic FHIR response, field-type errors surface at build time.

Python's AI/ML ecosystem (Anthropic SDK, spaCy, pandas) is stronger for the extraction and normalization pipelines. Writing these in C# would mean fewer library options and slower iteration.

Aspire bridges both: `AddUvicornApp()` manages Python services as first-class citizens with the same service discovery, health checks, and OpenTelemetry tracing as C# projects. One `dotnet run --project src/AppHost` starts everything.

## Consequences

- C# and Python services share no in-process code. Communication is via HTTP APIs and PostgreSQL (DoltgreSQL).
- The shared domain model (`Acme.Stack.Core`) is C#-only. Python services work with the same schema via database tables and API contracts.
- Developers need familiarity with both ecosystems. At a small team, this is acceptable — early engineers are generalists.
- If the team grows and specializes, the service boundaries allow teams to own their language stack independently.

## Links

- Implemented in: initial scaffold commit (AppHost `Program.cs`)
