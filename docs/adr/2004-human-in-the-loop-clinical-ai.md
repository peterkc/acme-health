# ADR-2004: Human-in-the-Loop for Clinical AI Extraction

## Status

Accepted

## Date

2026-03-16

## Context

The clinical-extractor service uses LLMs to extract structured data from unstructured clinical notes: medications, diagnoses (ICD-10 codes), procedures (CPT codes), and lab values (LOINC codes). These extracted fields become part of the patient record.

LLMs hallucinate. In a general-purpose application, a hallucinated fact is an inconvenience. In a clinical context, a hallucinated medication name or dosage is a patient safety risk.

## Decision

Every AI-derived clinical field requires a confidence score. Fields below a configurable threshold enter a human review queue. No AI extraction commits to the patient record without either high confidence or human sign-off.

## Considered Options

### 1. Trust LLM output directly

Auto-commit all AI extractions to the patient record without human review. Rely on model accuracy and post-hoc auditing to catch errors.

**Rejected because**: LLMs hallucinate. A hallucinated medication or dosage is a patient safety risk, not an inconvenience. Post-hoc auditing discovers errors after they have already entered the clinical record and potentially influenced care decisions.

### 2. Human review queue with confidence routing (chosen)

Every AI extraction carries a confidence score. High-confidence fields auto-commit; low-confidence fields enter a human review queue. No extraction commits without either high confidence or human sign-off.

## Architecture

Four components enforce this constraint:

### 1. Confidence scoring

Every extracted entity carries a confidence score (0.0-1.0) based on:
- Model self-reported confidence (logprobs where available)
- Source text span match quality (does the extracted term appear verbatim?)
- Coding system validation (is the suggested ICD-10 code a real code?)

### 2. Provenance chain

Every extracted field traces back to the source text. The extraction record stores:
- `source_document_id`: Reference to the original clinical note
- `source_text_span`: Character offset range in the source text
- `extraction_model`: Which LLM produced this extraction
- `extraction_timestamp`: When the extraction ran

### 3. Human review queue

Fields with confidence below threshold (default: 0.85) enter a review queue. A clinician or trained reviewer sees the extracted field alongside the source text and can:
- **Approve**: Field commits to patient record as-is
- **Correct**: Reviewer provides the correct value; both the AI extraction and correction are stored
- **Reject**: Field is discarded; source text is flagged for manual entry

### 4. Contradiction detection

Before committing any extraction (high-confidence or reviewed), the system checks for contradictions against existing patient records:
- Medication already discontinued but extracted as current
- Diagnosis code that conflicts with patient demographics (pediatric code on adult)
- Lab value outside physiological range

Contradictions enter the review queue regardless of confidence score.

## Rationale

The alternative — trusting LLM output for clinical data — is not viable for a health platform handling real patient records. The human-in-the-loop design adds latency to the extraction pipeline but eliminates the class of errors where AI-generated clinical data enters the patient record unchecked.

This is a first-class architectural component, not a quality filter added later. The review queue, provenance chain, and contradiction detection are designed into the data model and API contracts from the start.

## Consequences

- Extraction is not real-time for low-confidence fields. A note submitted at 3 PM may not have all extracted entities committed until a reviewer acts.
- The review queue needs a UI (out of scope for initial scaffold, but the API contract is defined).
- Storage cost increases: every extraction stores the full provenance chain, not just the final field value.
- Audit trails are comprehensive: every field in the patient record can answer "where did this come from?" — either a FHIR API source, a reviewed AI extraction, or a manual entry.

## Links

- Implemented in: `src/services/clinical-extractor/` (`POST /extract`, `POST /review`)
- Extended by: [peterkc/acme-health#1](https://github.com/peterkc/acme-health/issues/1) (HITL active learning epic)
- Research: [clinical-review-hitl](https://github.com/peterkc/acme-sdk/tree/research/clinical-review-hitl)
