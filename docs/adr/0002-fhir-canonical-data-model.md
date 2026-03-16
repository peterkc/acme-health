# ADR-0002: FHIR R4 as Canonical Data Model

## Status

Accepted

## Context

The platform ingests health data from multiple sources: EHR systems (via FHIR APIs), wearable devices (proprietary APIs), lab results (HL7v2, CSV), and clinical notes (PDF, free text). These sources use different schemas, coding systems, and data structures.

A canonical internal data model is needed so that all services — ingestion, API, AI extraction — work with a single representation of patient data.

## Decision

Use FHIR R4 resources as the canonical internal data model, with typed extension points for data FHIR does not cover (wearable-specific metrics, AI confidence scores, extraction provenance).

## Options Considered

### 1. FHIR R4 as-is

Map all data into standard FHIR resources. Patient, Observation, Condition, MedicationRequest, etc.

**Rejected because**: FHIR does not define resources for CGM glucose streams at 5-minute intervals, Apple HealthKit activity summaries, or AI extraction confidence scores. Forcing these into generic Observation resources loses semantic specificity.

### 2. Proprietary schema

Design a custom data model optimized for the platform's specific use cases.

**Rejected because**: Reinvents what FHIR already defines for 80% of health data. Breaks regulatory alignment (21st Century Cures Act mandates FHIR APIs). Makes future ONC certification harder. Every integration partner expects FHIR.

### 3. FHIR R4 with extensions (chosen)

FHIR R4 resources for standard health data. Typed extensions for wearable metrics, confidence scores, and provenance chains.

## Rationale

FHIR R4 is the regulatory-mandated future. The 21st Century Cures Act and HTI-1 Final Rule require FHIR APIs for patient data access. Building on FHIR from day one means:

- ONC Health IT certification (if pursued) doesn't require a data model migration
- Integration with Epic, Oracle Health, and athenahealth uses standard resource types
- Terminology systems (ICD-10, SNOMED CT, LOINC, CPT) map directly to FHIR CodeableConcept

For data FHIR doesn't cover:

- **CGM glucose streams**: Custom `Observation` profile with 5-minute interval semantics and device metadata (Dexcom G6, Libre 3)
- **Wearable activity summaries**: Custom `Observation` profile for aggregated step counts, heart rate zones, sleep stages
- **AI extraction confidence**: Extension on any resource field — `confidence: float`, `source_span: string`, `reviewer: Reference<Practitioner>?`

The Firely SDK (C#) supports FHIR profiles and extensions as first-class types, so these extensions get the same compile-time safety as standard resources.

## Consequences

- All services read and write FHIR resources. The database schema reflects FHIR resource structure.
- Custom extensions need documentation and validation rules (FHIR StructureDefinition).
- Python services work with FHIR data as JSON — no Firely SDK equivalent, so validation happens at the C# boundary.
- Future interoperability with other FHIR systems requires mapping custom extensions back to standard profiles (or registering them with a FHIR registry).
