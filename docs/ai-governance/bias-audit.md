# Bias Audit Framework

> Demographic stratification framework for the clinical entity extractor.
> Synthea synthetic data enables bias measurement without PHI risk.
> No evaluation has been run — this document defines the measurement approach.

## Why Synthea Enables Bias Measurement

Bias auditing requires known demographics. Real patient data carries PHI risk. Synthea solves both problems: it generates patients with configurable demographics (age, gender, race, ethnicity) based on CDC and NIH population statistics.

The [`data/README.md`](../../data/README.md) describes the Synthea dataset: ~1,000 synthetic patients in FHIR R4 format. Each `Patient` resource includes demographic fields that serve as stratification dimensions.

The trade-off is realism. Synthea generates clinical data from statistical models, not real clinical encounters. The demographic distribution reflects US population statistics, but the clinical notes lack the linguistic variation found in real documentation. This framework measures bias in the system under evaluation conditions — production monitoring would need real-world data.

## Stratification Dimensions

Demographics available in the FHIR R4 `Patient` resource and mapped to the `PatientRecord` model ([`Models.cs:7-13`](../../src/Acme.Stack.Core/Models.cs#L7)):

| Dimension | FHIR Field | Current Data Model | Status |
|-----------|------------|-------------------|--------|
| Age group | `Patient.birthDate` | `PatientRecord.BirthDate` | Available — derive age bands (0-17, 18-44, 45-64, 65+) |
| Gender | `Patient.gender` | `PatientRecord.Gender` | Available — maps to FHIR `AdministrativeGender` |
| Race | `Patient.extension[us-core-race]` | Not mapped | Gap — needs extension to PatientRecord |
| Ethnicity | `Patient.extension[us-core-ethnicity]` | Not mapped | Gap — needs extension to PatientRecord |

**Gap**: The current `PatientRecord` captures `Gender` but not race or ethnicity. These exist in the Synthea FHIR Bundle as US Core extensions on the `Patient` resource. Adding `Race` and `Ethnicity` fields to `PatientRecord` and updating the mapping in [`Program.cs:208-221`](../../src/Acme.Stack.FhirIngest/Program.cs#L208) would enable full demographic stratification.

## Metrics

### Demographic Parity Difference (DPD)

Measures whether the extraction success rate differs across demographic groups.

```
DPD = max(success_rate_by_group) - min(success_rate_by_group)
```

A DPD of 0 means all groups have identical extraction success rates. Higher values indicate disparity. There is no universal threshold — CMS and HITRUST have not published specific DPD limits for clinical AI. Values above 0.1 (10 percentage point gap) warrant investigation.

**Example**: If medication extraction succeeds 92% of the time for patients aged 18-44 but only 78% for patients aged 65+, the DPD is 0.14 — suggesting the model handles geriatric medication lists differently than younger patients' simpler regimens.

### Equalized Odds Ratio (EOR)

Measures whether error rates are consistent across demographic groups. DPD catches outcome gaps; EOR catches the case where overall accuracy looks equal but error types differ.

```
EOR = max(FPR_by_group) / min(FPR_by_group)
```

Where FPR is the false positive rate (incorrect extractions accepted as correct). An EOR of 1.0 means all groups have identical false positive rates. Values above 1.5 indicate that one group is disproportionately affected by incorrect extractions.

**Why both metrics**: A system could have equal overall accuracy (low DPD) but hallucinate medications more often for one demographic group (high EOR). Both metrics are needed to surface different failure modes.

## Evaluation Plan

When the clinical-extractor is implemented, evaluation follows this process:

### Step 1: Establish Ground Truth

Use MTSamples (4,999 clinical notes, 40 specialties) with manually annotated entities as ground truth. MTSamples provides real clinical language variety; Synthea provides the demographic stratification labels.

### Step 2: Run Extraction

Process notes through the clinical-extractor, collecting raw extraction output with confidence scores.

### Step 3: Stratify Results

For each demographic dimension:
1. Group patients by dimension values (e.g., age bands)
2. Calculate precision, recall, and F1 per group
3. Calculate DPD across groups
4. Calculate EOR (false positive rate ratio) across groups
5. Break down by entity type — medication extraction bias may differ from diagnosis extraction bias

### Step 4: Report

Per-dimension summary table:

| Dimension | Group | Precision | Recall | F1 | FPR |
|-----------|-------|-----------|--------|-----|-----|
| Age | 0-17 | — | — | — | — |
| Age | 18-44 | — | — | — | — |
| Age | 45-64 | — | — | — | — |
| Age | 65+ | — | — | — | — |
| **DPD** | | | | **—** | |
| **EOR** | | | | | **—** |

One table per entity type (medications, diagnoses, procedures, lab values). Empty cells reflect the pre-implementation state.

## Known Limitations

1. **Synthea reflects population statistics, not clinical practice variation**. A 65-year-old Synthea patient has the right comorbidity distribution but not the abbreviated shorthand a busy hospitalist uses when documenting. Bias in note *writing style* is not captured.

2. **MTSamples has specialty metadata but limited demographic metadata**. We can measure "does the model work differently on cardiology notes vs. psychiatry notes?" but cannot stratify MTSamples by patient demographics directly. The Synthea-MTSamples pairing is a workaround, not ideal.

3. **Synthea uses US demographics only**. The framework does not address bias for non-US patient populations, multilingual notes, or non-Western medical terminology.

4. **Intersectional analysis is limited by sample size**. With 1,000 Synthea patients, stratifying by age × gender × race produces groups too small for statistical significance. Larger Synthea populations (10,000+) would be needed for intersectional metrics.

## Regulatory Context

Three regulatory frameworks are driving bias auditing requirements for healthcare AI:

- **CMS Medicare Advantage guidance** (2024): AI cannot serve as the sole basis for coverage denials. MAOs must understand biased inputs and regularly audit AI systems.
- **Colorado AI Act** (effective June 30, 2026): Requires impact assessments and bias risk disclosure to the AG for high-risk AI making consequential healthcare decisions.
- **NYC Local Law 144** (active since July 2023): Annual bias audits for automated decision tools. While focused on employment, it establishes precedent for healthcare AI auditing.

See [`research/healthcare-compliance/03-ai-ml-regulations.md`](../../research/healthcare-compliance/03-ai-ml-regulations.md) for the full regulatory landscape.
