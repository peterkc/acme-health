# NIST AI Risk Management Framework Alignment

> Maps the four functions of [NIST AI RMF 1.0](https://www.nist.gov/itl/ai-risk-management-framework) to ACME Health project decisions.
> Demo project — maturity assessment is honest about what is implemented vs. designed.

## Framework Summary

The NIST AI Risk Management Framework organizes AI governance into four functions: **Govern**, **Map**, **Measure**, **Manage**. Each function addresses a different aspect of AI risk across the system lifecycle.

This document maps each function to specific decisions made in the ACME Health platform, referencing ADRs, code paths, and design documents rather than restating framework requirements.

## GOVERN — Policies and Accountability

Governance decisions that constrain how AI operates in the system.

### HITL as Architectural Constraint

[ADR-2004](../adr/2004-human-in-the-loop-clinical-ai.md) makes human review a first-class component, not an optional filter. The decision was made before writing any extraction code — the review queue, provenance chain, and contradiction detection are designed into the data model and API contracts.

The confidence threshold (default 0.85) is configurable per deployment. This means the governance boundary can be tightened (lower threshold = more human review) without code changes.

### Type Safety Where Correctness Matters

[ADR-2001](../adr/2001-polyglot-aspire-orchestration.md) chose C# with the Firely SDK for FHIR parsing specifically because FHIR R4 has nested resource types where a wrong field type can mean a wrong medication dose. The compiler catches these errors; a dynamic language would surface them at runtime — possibly in production.

This is a governance decision expressed as a technology choice: the cost of type safety (C# complexity, Firely SDK learning curve) is justified by the reduced risk of malformed clinical data entering the system.

### Provenance by Default

Every AI-extracted entity stores its full provenance chain (ADR-2004, Section 2): source document ID, character offset, extraction model, and timestamp. This is not optional or configurable — provenance is part of the extraction data model.

The design principle: if you cannot trace an extracted medication back to the sentence that produced it, the extraction should not exist in the patient record.

## MAP — Risk Identification

Risks identified and mapped to mitigation strategies.

### Hallucination

LLMs generate plausible clinical data that may be incorrect. A hallucinated ICD-10 code creates a false diagnosis in the patient record.

**Mitigation**: Three-layer defense (ADR-2004):
1. Confidence scoring — model self-report combined with source text span matching and code system validation
2. Human review queue — low-confidence extractions require sign-off
3. Contradiction detection — extracted entities checked against existing patient records

### Bias

Models trained on English-language medical literature may perform differently across patient demographics, clinical specialties, and note formats.

**Mitigation**: Demographic stratification framework using Synthea's known patient demographics. See [bias-audit.md](bias-audit.md) for metrics (DPD, EOR) and evaluation plan.

### Prompt Sensitivity

Clinical notes vary in format: structured templates, free-text dictation, abbreviated shorthand, specialty-specific terminology. The same clinical fact expressed differently may produce different extraction results.

**Mitigation**: Evaluation across MTSamples' 40 medical specialties to identify specialty-specific accuracy gaps. Confidence scoring includes source text span matching — if the extracted term does not appear verbatim in the source, confidence decreases.

### Data Leakage

Clinical note text crosses the trust boundary to the Anthropic API (TB-3 in [threat-model.md](../architecture/threat-model.md)).

**Mitigation**: Anthropic's data policy does not use API inputs for training. Production requires a BAA with Anthropic. Patient consent for AI processing of clinical notes is addressed in [consent-architecture.md](consent-architecture.md).

## MEASURE — Evaluation Framework

How AI risks are quantified and tracked.

### Planned Metrics

| Metric | What It Measures | Data Source |
|--------|-----------------|-------------|
| Accuracy by entity type | Precision/recall for medications, diagnoses, procedures, lab values | MTSamples (4,999 notes) |
| Demographic Parity Difference (DPD) | Extraction success rate gap across demographic groups | Synthea (1,000 patients) |
| Equalized Odds Ratio (EOR) | Error rate consistency across demographic groups | Synthea (1,000 patients) |
| Confidence calibration | Does reported confidence match actual accuracy? | Calibration curves by entity type |
| Coding accuracy | Are extracted ICD-10/CPT/LOINC codes valid and correct? | Code system validation lookups |

See [bias-audit.md](bias-audit.md) for DPD and EOR formulas and stratification dimensions.

### Data Sources

- **MTSamples**: 4,999 clinical transcriptions across 40 medical specialties. Real clinical language variety without PHI (CC0 license). Limitations: limited demographic metadata, specialty distribution not representative of a general hospital.
- **Synthea**: 1,000 synthetic patients with known demographics (age, gender, race/ethnicity from FHIR Patient resource). Known limitations: generated clinical notes may lack the linguistic variety of real notes.

See [`data/README.md`](../../data/README.md) for the complete dataset inventory.

### Current State

No evaluation has been run. The clinical-extractor is a stub. The metrics above define what will be measured when extraction logic is implemented.

## MANAGE — Ongoing Controls

How risks are monitored and responded to in operation.

### Version-Controlled Data Mutations

Every change to the patient record — whether from FHIR ingest, CGM normalization, or AI extraction — creates a Dolt commit. This provides:

- **Rollback capability**: A bad extraction batch can be reverted to the pre-ingest state via `DOLT_CHECKOUT`
- **Drift detection**: `dolt_diff()` between time periods shows whether extraction patterns are changing
- **Forensic analysis**: The commit log reconstructs the complete history of any patient record

See [data-lineage.md](../architecture/data-lineage.md) for query examples.

### Provenance Chain

ADR-2004 requires every extracted field to trace back to source text. If a clinician questions an extracted medication, the system shows the exact sentence that produced it, when the extraction ran, and which model produced it.

### Human Review Queue

Low-confidence extractions (below 0.85) do not enter the patient record until a human reviewer approves, corrects, or rejects them. The review decision is stored alongside the extraction, creating an audit trail of human judgment applied to AI output.

### Contradiction Detection

Before any extraction commits — whether auto-committed (high confidence) or human-reviewed — the system checks for contradictions against existing patient data. A medication marked as discontinued but extracted as current triggers a review, regardless of confidence score.

## Maturity Assessment

| Function | Status | Evidence |
|----------|--------|----------|
| **GOVERN** | Embedded in architecture | ADR-2004 (HITL), ADR-2001 (type safety), provenance-by-default data model |
| **MAP** | Risks identified and mapped to mitigations | Hallucination → 3-layer defense, bias → stratification framework, data leakage → threat model TB-3 |
| **MEASURE** | Designed, not operational | Metrics defined, data sources identified, no evaluations run |
| **MANAGE** | Partially operational | Dolt audit trail is active; review queue, provenance chain, and contradiction detection are designed but not built |

GOVERN and MAP decisions are embedded in the architecture and ADRs. These persist regardless of implementation status. MEASURE and MANAGE depend on the clinical-extractor being operational — the framework is ready, the data is not.

## References

- [NIST AI RMF 1.0](https://www.nist.gov/itl/ai-risk-management-framework)
- [NIST AI 600-1](https://nvlpubs.nist.gov/nistpubs/ai/NIST.AI.600-1.pdf) — Generative AI risk supplement (covers confabulation, data privacy, bias)
- [ADR-2004: Human-in-the-Loop for Clinical AI](../adr/2004-human-in-the-loop-clinical-ai.md)
- [Model Card](model-card.md)
- [Bias Audit Framework](bias-audit.md)
