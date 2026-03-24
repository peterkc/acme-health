# SDOH Feature Rationale

> Documents the inclusion and exclusion decisions for social determinants of health data in the clinical AI pipeline.
> Not yet implemented — this document describes the design rationale.

## What SDOH Means for Clinical AI

Social determinants of health — housing stability, food security, employment, education, transportation access — account for 30-55% of health outcomes according to WHO estimates. ICD-10-CM encodes these as Z-codes (Z55-Z65):

| Z-code Range | Category | Example |
|-------------|----------|---------|
| Z55 | Education and literacy | Z55.0 — Illiteracy |
| Z56 | Employment | Z56.0 — Unemployment |
| Z57 | Occupational exposure | Z57.1 — Radiation exposure |
| Z59 | Housing and economics | Z59.0 — Homelessness |
| Z60 | Social environment | Z60.2 — Living alone |
| Z62 | Upbringing | Z62.810 — Personal history of child abuse |
| Z63 | Family circumstances | Z63.0 — Relationship distress |
| Z65 | Psychosocial | Z65.1 — Imprisonment |

The [Gravity Project](https://hl7.org/fhir/us/sdoh-clinicalcare/) publishes the authoritative FHIR implementation guide for SDOH clinical care, mapping screening instruments (LOINC-coded) to assessments, goals, and interventions.

## Inclusion Decision

SDOH data should be part of the clinical data model. Ignoring it creates two problems:

**Clinical accuracy**: A diabetes management model that does not account for food insecurity will generate recommendations the patient cannot follow. SDOH features improve the clinical relevance of AI output, not just its statistical accuracy.

**Bias amplification**: SDOH factors correlate with demographic characteristics. A model that excludes SDOH and instead learns demographic proxies for these factors encodes the correlation without the clinical context. Including SDOH explicitly gives the model the actual signal rather than a proxy.

### Where SDOH Fits in the Data Model

[ADR-2002](../adr/2002-fhir-canonical-data-model.md) establishes FHIR R4 as the canonical data model. SDOH data maps to existing FHIR resource types:

| SDOH Concept | FHIR Resource | Coding |
|-------------|---------------|--------|
| Screening result | `Observation` | LOINC (screening instrument codes) |
| Assessed condition | `Condition` | ICD-10-CM Z-codes |
| Care goal | `Goal` | SNOMED CT |
| Intervention | `ServiceRequest` | SNOMED CT + CPT |

The `observations` table in Dolt MySQL ([`Models.cs:19-27`](../../src/Acme.Stack.Core/Models.cs#L19)) can store SDOH observations using the same `code`/`display`/`value` structure as clinical observations. No schema change is required — Z-codes work as `Observation.code` values.

### Source of SDOH Data

Z-codes appear in clinical notes, not just structured data entry. Nursing documentation frequently contains social history that maps to Z-codes: "Patient reports being unhoused for the past 3 months" → Z59.0.

The clinical-extractor ([`main.py:11-18`](../../src/services/clinical-extractor/main.py#L11)) should extract SDOH mentions alongside medications, diagnoses, and procedures. The extraction pipeline needs SDOH-specific evaluation metrics — a model that extracts "homelessness" but assigns the wrong Z-code is a different failure mode than one that misses the mention entirely.

## Exclusion Decision

SDOH data should **not** be used as a proxy for race in risk scoring.

The known failure: the Epic sepsis prediction model systematically underperformed for Black patients. CNNs trained on public chest X-ray datasets underdiagnose Black, Hispanic, and Medicaid patients. In both cases, the models learned demographic correlations that substituted for clinical features.

SDOH features can recreate this failure. Homelessness, food insecurity, and unemployment correlate with race and ethnicity in the US. A risk model that uses SDOH features without guard rails will learn the same demographic proxies that direct demographic features would encode.

### Guard Rails

1. **SDOH informs care coordination, not coverage denial**. SDOH data should influence recommendations ("this patient may need transportation assistance for follow-up appointments") rather than risk scores that affect coverage ("this patient is higher risk, deny the procedure").

2. **Stratified evaluation is mandatory**. Any model that uses SDOH features must report accuracy stratified by demographic group (see [bias-audit.md](bias-audit.md)). If adding SDOH features improves accuracy for some groups but decreases it for others, the feature inclusion is causing harm.

3. **No SDOH-only risk scoring**. SDOH data supplements clinical data; it does not replace it. A risk score based primarily on social factors rather than clinical findings is a screening tool, not a clinical prediction.

## Privacy Considerations

SDOH data reveals sensitive life circumstances. Housing instability, incarceration history, substance use, and family problems carry stigma and can affect employment, insurance, and legal outcomes.

### Washington MHMDA Intersection

The Washington My Health My Data Act (active, no size threshold) defines "health data" broadly enough to include SDOH information — particularly data about health conditions, social circumstances, and health-seeking behavior. For a platform accessible to Washington state residents, SDOH data falls under MHMDA's private right of action.

This means:
- Consent for SDOH data collection must be explicit
- Purpose limitation applies (collected for care coordination, not sold to third parties)
- The geofencing prohibition near healthcare facilities may affect location-derived SDOH inferences

### Consent Granularity

The [consent architecture](consent-architecture.md) should treat SDOH data as a separate consent category. A patient might consent to sharing clinical lab results but not their housing status. The FHIR Consent resource supports this through `provision.class` restrictions on specific resource types.

## Current Implementation

SDOH extraction and Z-code integration are not yet implemented.

What exists today:
- Synthea generates some SDOH-related conditions (based on CDC prevalence data)
- MTSamples clinical notes include social history sections that mention SDOH factors
- The `observations` table schema can store Z-coded observations without modification

What needs to be built:
- SDOH-specific extraction prompts for the clinical-extractor
- Z-code validation in the confidence scoring pipeline
- SDOH-stratified evaluation metrics alongside the demographic stratification in [bias-audit.md](bias-audit.md)
- Consent enforcement for SDOH data access (separate from general clinical consent)

## References

- [Gravity Project SDOH Clinical Care IG](https://hl7.org/fhir/us/sdoh-clinicalcare/)
- [ADR-2002: FHIR R4 Canonical Data Model](../adr/2002-fhir-canonical-data-model.md)
- [Bias Audit Framework](bias-audit.md) — demographic stratification metrics
- [Consent Architecture](consent-architecture.md) — consent granularity for SDOH
- [`research/healthcare-compliance/05-emerging-standards.md`](../../research/healthcare-compliance/05-emerging-standards.md) — SDOH and Gravity Project research
