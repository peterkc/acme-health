#!/usr/bin/env bash
set -euo pipefail

DATA_DIR="$(cd "$(dirname "$0")/.." && pwd)"

echo "=== Synthea FHIR R4 Sample Data ==="
echo "Downloading 1K patient bundles (~90MB)..."
mkdir -p "$DATA_DIR/synthea"
curl -sSL -o "$DATA_DIR/synthea/synthea_fhir_r4.zip" \
  "https://synthetichealth.github.io/synthea-sample-data/downloads/latest/synthea_sample_data_fhir_r4_sep2019.zip"
echo "Downloaded. Unzip: cd $DATA_DIR/synthea && unzip synthea_fhir_r4.zip -d fhir/"

echo ""
echo "=== MTSamples Medical Transcriptions ==="
if command -v kaggle &> /dev/null; then
  kaggle datasets download -d tboyle10/medicaltranscriptions -p "$DATA_DIR/mtsamples/"
  echo "Downloaded to $DATA_DIR/mtsamples/"
else
  echo "Kaggle CLI not installed. Download manually from:"
  echo "  https://www.kaggle.com/datasets/tboyle10/medicaltranscriptions"
fi

echo ""
echo "=== GlucoBench CGM Data ==="
echo "Clone from: https://github.com/IrinaStatsLab/GlucoBench"
echo "  git clone https://github.com/IrinaStatsLab/GlucoBench.git /tmp/glucobench"
echo "  cp /tmp/glucobench/raw_data.zip $DATA_DIR/cgm/"

echo ""
echo "=== MIMIC-IV FHIR Demo ==="
echo "100 patients, no credentials needed."
echo "  Download from: https://physionet.org/content/mimic-iv-fhir-demo/2.1.0/"

echo ""
echo "Done. See data/README.md for full inventory."
