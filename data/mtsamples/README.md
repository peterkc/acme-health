# MTSamples — Medical Transcription Samples

4,999 de-identified medical transcription samples across 40 specialties. Used to develop and test the clinical-extractor LLM pipeline.

## Download

```bash
# Requires Kaggle CLI (pip install kaggle)
kaggle datasets download -d tboyle10/medicaltranscriptions -p .
unzip medicaltranscriptions.zip
```

Or download directly from: https://www.kaggle.com/datasets/tboyle10/medicaltranscriptions

## Format

CSV with columns: `description`, `medical_specialty`, `sample_name`, `transcription`, `keywords`

Specialties include: cardiology, neurology, orthopedics, general medicine, gastroenterology, radiology, and 34 others.

## License

CC0 Public Domain — no restrictions on use.
