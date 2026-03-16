# CGM (Continuous Glucose Monitor) Datasets

Glucose readings from continuous glucose monitors, used to develop and test the wearable-normalizer pipeline.

## GlucoBench (Primary)

Consolidated and standardized CGM datasets with ML benchmarks.

```bash
git clone https://github.com/IrinaStatsLab/GlucoBench.git /tmp/glucobench
cp /tmp/glucobench/raw_data.zip .
unzip raw_data.zip -d raw/
```

Source: https://github.com/IrinaStatsLab/GlucoBench

## Additional Sources

| Dataset | Subjects | Device | Notes |
|---------|----------|--------|-------|
| D1NAMO | 29 (20 healthy + 9 T1D) | Various | CGM + ECG + accelerometry |
| AI-READI | Varies | Dexcom G6 | 10 days, 5-min intervals, paired with Garmin |
| PhysioCGM | Varies | Dexcom G6 | CGM + ECG/PPG/EDA |

See [Awesome-CGM](https://github.com/IrinaStatsLab/Awesome-CGM) for a curated index.

## Format

CSV with time-stamped glucose readings (mg/dL), typically at 5-minute intervals for Dexcom devices.

## License

Varies by dataset — most are CC 4.0 or open-access research licenses.
