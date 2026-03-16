# Synthea FHIR R4 Sample Data

Synthetic patient population from [MITRE Synthea](https://synthea.mitre.org/). Each patient is a FHIR R4 Bundle containing encounters, conditions, medications, observations (LOINC-coded lab values), and procedures.

## Download

```bash
# Pre-generated 1K patients (~90MB)
curl -sSL -o synthea_fhir_r4.zip \
  "https://synthetichealth.github.io/synthea-sample-data/downloads/latest/synthea_sample_data_fhir_r4_sep2019.zip"
unzip synthea_fhir_r4.zip -d fhir/
```

Or generate custom populations:
```bash
# Requires Java 11+
git clone https://github.com/synthetichealth/synthea.git /tmp/synthea
cd /tmp/synthea && ./run_synthea -p 100 --exporter.fhir.export true
```

## Format

Each file is a FHIR Bundle (type: `transaction`) containing one patient's complete history. Observation resources include LOINC-coded lab values — no separate lab dataset needed.

## License

Apache 2.0
