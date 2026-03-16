# Sample Datasets

Public datasets used for development and demonstration. Large files are gitignored; this directory contains download instructions and metadata.

## Inventory

| Dataset | Records | Format | License | Service |
|---------|---------|--------|---------|---------|
| Synthea FHIR R4 | ~1,000 patients | FHIR Bundle JSON | Apache 2.0 | FhirIngest |
| MTSamples | 4,999 transcriptions | CSV | CC0 Public Domain | clinical-extractor |
| GlucoBench | ~479 patients (5 datasets) | CSV | CC 4.0 / GPL-2 | wearable-normalizer |
| MIMIC-IV FHIR Demo | 100 patients | FHIR R4 | ODbL | FhirIngest |
| Harvard Dataverse | Apple Watch + Fitbit | CSV | CC BY 4.0 | wearable-normalizer |

## Download

```bash
bash scripts/download-samples.sh
```

Or download individual datasets — see README files in each subdirectory.

## Sources

- [Synthea](https://synthea.mitre.org/downloads) — Synthetic patient generator from MITRE
- [MTSamples](https://www.kaggle.com/datasets/tboyle10/medicaltranscriptions) — Medical transcription samples (Kaggle)
- [GlucoBench](https://github.com/IrinaStatsLab/GlucoBench) — Consolidated CGM datasets
- [MIMIC-IV FHIR Demo](https://physionet.org/content/mimic-iv-fhir-demo/2.1.0/) — De-identified ICU data in FHIR format
- [Harvard Dataverse](https://doi.org/10.7910/DVN/ZS2Z2J) — Apple Watch + Fitbit activity data
