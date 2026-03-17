"""Unit tests for CGM CSV parsing — no database needed."""

import pytest

from main import parse_cgm_csv, parse_connection_string


class TestParseConnectionString:
    """Test .NET connection string conversion to psycopg DSN."""

    def test_standard_format(self):
        raw = "Host=localhost;Port=5432;Username=root;Database=acme_health"
        result = parse_connection_string(raw)
        assert "host=localhost" in result
        assert "port=5432" in result
        assert "user=root" in result
        assert "dbname=acme_health" in result

    def test_with_password(self):
        raw = "Host=db;Port=5432;Username=admin;Password=secret;Database=mydb"
        result = parse_connection_string(raw)
        assert "password=secret" in result
        assert "user=admin" in result

    def test_trailing_semicolons(self):
        raw = "Host=localhost;Port=5432;Username=root;Database=acme_health;"
        result = parse_connection_string(raw)
        assert "host=localhost" in result

    def test_case_insensitive_keys(self):
        raw = "host=localhost;port=5432;username=root;database=acme_health"
        result = parse_connection_string(raw)
        assert "host=localhost" in result
        assert "user=root" in result


class TestParseCgmCsv:
    """Unit tests for CSV parsing and timestamp normalization."""

    def test_parse_valid_csv(self, sample_csv: bytes):
        df, warnings = parse_cgm_csv(sample_csv, "sample.csv")
        assert len(df) == 5
        assert warnings == 0
        assert list(df.columns) == ["timestamp_utc", "glucose_mg_dl", "source_file"]

    def test_glucose_values_preserved(self, sample_csv: bytes):
        df, _ = parse_cgm_csv(sample_csv, "sample.csv")
        glucose_values = df["glucose_mg_dl"].tolist()
        assert glucose_values == [95.0, 97.0, 102.0, 110.0, 118.0]

    def test_source_file_recorded(self, sample_csv: bytes):
        df, _ = parse_cgm_csv(sample_csv, "test_file.csv")
        assert all(df["source_file"] == "test_file.csv")

    def test_malformed_rows_skipped(self, malformed_csv: bytes):
        df, warnings = parse_cgm_csv(malformed_csv, "malformed.csv")
        # 6 rows total: row 2 (bad ts), row 3 (bad glucose), row 5 (both empty)
        # Valid rows: 1, 4, 6 = 3 valid
        assert len(df) == 3
        assert warnings > 0

    def test_timestamps_normalized_to_utc(self, mixed_tz_csv: bytes):
        df, warnings = parse_cgm_csv(mixed_tz_csv, "mixed_tz.csv")
        assert len(df) == 4
        assert warnings == 0
        # All timestamps should be UTC
        for ts in df["timestamp_utc"]:
            assert ts.tzname() == "UTC"

    def test_timezone_conversion_correct(self, mixed_tz_csv: bytes):
        df, _ = parse_cgm_csv(mixed_tz_csv, "mixed_tz.csv")
        timestamps = df["timestamp_utc"].tolist()
        # 2024-01-15T08:00:00+02:00 -> 2024-01-15T06:00:00Z
        assert timestamps[0] == timestamps[1]  # Both should be 06:00 UTC
        # 2024-01-15 01:00:00-05:00 -> 2024-01-15T06:00:00Z
        assert timestamps[0] == timestamps[2]  # Also 06:00 UTC

    def test_empty_csv_returns_empty_df(self):
        content = b"timestamp,glucose_mg_dl\n"
        df, warnings = parse_cgm_csv(content, "empty.csv")
        assert len(df) == 0
        assert warnings == 0

    def test_missing_timestamp_column_raises(self):
        content = b"when,glucose_mg_dl\n2024-01-15T06:00:00Z,95\n"
        with pytest.raises(ValueError, match="No timestamp column found"):
            parse_cgm_csv(content, "bad.csv")

    def test_missing_glucose_column_raises(self):
        content = b"timestamp,reading\n2024-01-15T06:00:00Z,95\n"
        with pytest.raises(ValueError, match="No glucose column found"):
            parse_cgm_csv(content, "bad.csv")

    def test_alternative_column_names(self):
        """Column names like 'time' and 'glucose' should be recognized."""
        content = b"time,glucose\n2024-01-15T06:00:00Z,95\n"
        df, warnings = parse_cgm_csv(content, "alt.csv")
        assert len(df) == 1
        assert warnings == 0

    def test_first_matching_column_wins(self):
        """When multiple column name variants exist, first in precedence order wins."""
        content = (
            b"timestamp,time,glucose_mg_dl\n"
            b"2024-01-15T06:00:00Z,2024-01-15T07:00:00Z,95\n"
        )
        df, warnings = parse_cgm_csv(content, "ambiguous.csv")
        assert len(df) == 1
        assert warnings == 0
        # "timestamp" should win over "time" (first in TIMESTAMP_COLUMNS tuple)
        assert df["timestamp_utc"].iloc[0].hour == 6
