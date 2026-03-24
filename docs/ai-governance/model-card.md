# Model Card: Clinical Entity Extractor

> Format follows [Mitchell et al. 2019](https://arxiv.org/abs/1810.03993).
> The clinical-extractor is a stub — this document describes the designed architecture per [ADR-2004](../adr/2004-human-in-the-loop-clinical-ai.md), not a deployed system.

## Model Details

**Type**: LLM-based clinical entity extraction via the Anthropic API (Claude). No fine-tuning — the model is used through prompt engineering with structured output parsing.

**Task**: Extract structured medical entities from unstructured clinical note text:

| Entity Type | Coding System | Example |
|-------------|---------------|---------|
| Medications | RxNorm | "metformin 500mg BID" → RxNorm 860974 |
| Diagnoses | ICD-10-CM | "type 2 diabetes" → E11.9 |
| Procedures | CPT | "comprehensive metabolic panel" → CPT 80053 |
| Lab values | LOINC | "HbA1c 7.2%" → LOINC 4548-4, value 7.2 |

Each extraction carries a confidence score (0.0-1.0) and a provenance chain linking the extracted entity to the source text span, extraction model, and timestamp (ADR-2004, Section 2).

**Endpoints**: [`POST /extract`](../../src/services/clinical-extractor/main.py#L11) and [`POST /review`](../../src/services/clinical-extractor/main.py#L21) — both currently return `{"status": "not_implemented"}`.

## Intended Use

Assist clinical documentation by extracting structured data from narrative text. A clinician or trained reviewer examines the extractions before they commit to the patient record.

This system targets FDA CDS Criterion 4 compliance: the human reviewer can independently evaluate each extraction against the source text. The tool provides recommendations; the clinician decides. See [FDA Regulatory Position](#fda-regulatory-position) below.

**Primary users**: Clinical documentation specialists, medical coders, quality reviewers.

**Primary beneficiaries**: Patients whose records become more complete and coded accurately.

## Out-of-Scope Uses

- Autonomous prescribing or medication ordering
- Standalone diagnosis without clinician review
- Real-time clinical alerting without human oversight
- Insurance coverage determination or prior authorization decisions
- Any use where the extraction output directly affects patient care without human review

## Factors and Subgroups

The following factors could affect extraction accuracy:

| Factor | Concern |
|--------|---------|
| Medical specialty | Cardiology notes use different terminology than psychiatry notes |
| Note format | Structured templates vs. free-text dictation vs. abbreviated shorthand |
| Abbreviation density | Dense abbreviations (common in ED notes) may reduce extraction accuracy |
| Language | English only; multilingual clinical notes are out of scope |
| Patient demographics | Age-specific terminology (pediatric vs. geriatric), gendered conditions |

## Data

### Evaluation Data (Available)

| Dataset | Records | Source | License | Use |
|---------|---------|--------|---------|-----|
| [MTSamples](https://www.kaggle.com/datasets/tboyle10/medicaltranscriptions) | 4,999 clinical transcriptions | Kaggle | CC0 Public Domain | Extraction accuracy by specialty |
| [Synthea](https://synthea.mitre.org/) FHIR R4 | ~1,000 synthetic patients | MITRE | Apache 2.0 | Demographic stratification of outcomes |

See [`data/README.md`](../../data/README.md) for download instructions and full dataset inventory.

### Training Data

Not applicable. The system uses the Anthropic API without fine-tuning. The model's training data is Anthropic's responsibility and is not controlled by this project.

## Metrics

### Planned Evaluation Framework

When the clinical-extractor is implemented, evaluation will measure:

1. **Accuracy by entity type**: Precision and recall for medications, diagnoses, procedures, and lab values independently. A system that extracts medications well but struggles with procedure codes should surface that distinction.

2. **Demographic stratification**: Extraction accuracy broken down by patient age group, gender, and race/ethnicity from the Synthea patient population. See [bias-audit.md](bias-audit.md) for the stratification framework and metrics (DPD, EOR).

3. **Confidence calibration**: When the model reports 0.85 confidence, are 85% of those extractions correct? Calibration curves plotted by entity type and confidence bucket.

4. **Coding accuracy**: For ICD-10, CPT, and LOINC codes specifically — is the extracted code a valid code in the coding system? Is it the correct code for the clinical concept?

### Thresholds

ADR-2004 sets a default confidence threshold of 0.85. Extractions above this threshold auto-commit; below it, they enter the human review queue. The threshold is configurable per deployment.

No production accuracy baselines exist. The evaluation framework above defines how they will be established.

## Ethical Considerations

### Hallucination Risk

LLMs generate plausible but incorrect text. In clinical contexts, a hallucinated medication name or dosage creates patient safety risk. ADR-2004 addresses this through three mechanisms:

1. **Confidence scoring**: Self-reported confidence combined with source text span matching and code system validation
2. **Human review queue**: Low-confidence extractions require human sign-off before committing
3. **Contradiction detection**: Extracted entities are checked against existing patient records for conflicts (e.g., medication listed as current but previously discontinued)

### Bias

Models trained on predominantly English-language medical literature may perform differently on notes describing conditions prevalent in specific demographic groups, notes written in clinical shorthand common to safety-net hospitals, or notes with cultural context that affects medical terminology. See [bias-audit.md](bias-audit.md) for the measurement approach.

### Transparency

Every extraction stores its provenance chain: source document ID, character offset in the source text, extraction model identifier, and timestamp (ADR-2004, Section 2). A reviewer can trace any extracted entity back to the exact text that produced it.

## FDA Regulatory Position

The FDA's Clinical Decision Support guidance (January 2026) defines four criteria for determining whether CDS software is a medical device. CDS is **not** a device if it:

1. Does not acquire, process, or analyze medical images, signals, or patterns
2. Displays, analyzes, or prints medical information
3. Provides recommendations to a healthcare professional
4. Enables the HCP to independently review the basis of recommendations

This system targets compliance with all four criteria:

- **Criterion 1**: Processes text, not medical images or signals
- **Criterion 2**: Displays extracted entities alongside source text
- **Criterion 3**: Provides extractions as recommendations to a clinical reviewer
- **Criterion 4**: The provenance chain and source text span enable independent review — the reviewer sees both the AI extraction and the exact text it came from

If the system were modified to auto-commit extractions without human review, or to provide time-critical recommendations that clinicians cannot meaningfully evaluate, it would likely fail Criterion 4 and require FDA premarket authorization.

No FDA authorization has been sought or obtained. This analysis describes the architectural intent, not a regulatory determination.

## Limitations

- **Stub implementation**: No extraction logic exists. The governance framework is designed but not operational.
- **No production evaluation data**: Accuracy metrics will be established when the extractor is built and evaluated against MTSamples.
- **Confidence scoring not implemented**: The three-component scoring approach (model self-report, span matching, code validation) from ADR-2004 has not been built.
- **Single language**: English only. Multilingual clinical notes are not addressed.
- **Contradiction detection not built**: ADR-2004 Section 4 describes this component; it does not yet exist.
- **No BAA with Anthropic**: Production use would require a Business Associate Agreement for sending clinical text to the Anthropic API.
