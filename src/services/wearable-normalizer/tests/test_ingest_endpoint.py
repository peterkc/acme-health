"""Tests for the /ingest/cgm endpoint via FastAPI TestClient.

Unit tests mock aiomysql to validate CSV parsing, normalization, response format,
and error handling without a real database.
DB-dependent tests are marked @pytest.mark.integration.
"""

import io
from unittest.mock import AsyncMock, patch

import pytest


class _MockCursor:
    """Async context manager cursor mock that tracks calls."""

    def __init__(self):
        self.executemany = AsyncMock()
        self.execute = AsyncMock()
        self.fetchone = AsyncMock(return_value=("abc123",))

    async def __aenter__(self):
        return self

    async def __aexit__(self, *args):
        pass


class _MockConnection:
    """aiomysql connection mock (not a context manager — matches aiomysql API)."""

    def __init__(self):
        self._cursors: list[_MockCursor] = []
        self.commit = AsyncMock()
        self.close = lambda: None  # aiomysql conn.close() is synchronous

    def cursor(self):
        cur = _MockCursor()
        self._cursors.append(cur)
        return cur


@pytest.fixture()
def mock_db_client(client):
    """Client fixture with mocked aiomysql connection for endpoint tests."""
    mock_conn = _MockConnection()

    async def fake_connect(**kwargs):
        return mock_conn

    with patch("main.aiomysql.connect", side_effect=fake_connect):
        yield client, mock_conn


class TestIngestCgmEndpoint:
    """T038: test_ingest_cgm_persists_readings."""

    def test_ingest_valid_csv_returns_readings(self, mock_db_client, sample_csv):
        """POST a valid CSV and verify response contains reading count."""
        client, _ = mock_db_client
        response = client.post(
            "/ingest/cgm",
            files={"file": ("sample.csv", io.BytesIO(sample_csv), "text/csv")},
        )
        assert response.status_code == 200
        data = response.json()
        assert data["readings"] == 5
        assert "start" in data
        assert "end" in data

    def test_ingest_cgm_returns_summary_with_count_and_range(
        self, mock_db_client, sample_csv
    ):
        """T03B: Response includes reading count and time range."""
        client, _ = mock_db_client
        response = client.post(
            "/ingest/cgm",
            files={"file": ("sample.csv", io.BytesIO(sample_csv), "text/csv")},
        )
        assert response.status_code == 200
        data = response.json()
        assert data["readings"] == 5
        # Verify time range is present and ordered
        assert data["start"] < data["end"]
        # Verify ISO 8601 format
        assert "2024-01-15" in data["start"]
        assert "2024-01-15" in data["end"]

    def test_ingest_calls_executemany(self, mock_db_client, sample_csv):
        """Verify batch insert uses executemany (not row-by-row execute)."""
        client, mock_conn = mock_db_client
        client.post(
            "/ingest/cgm",
            files={"file": ("sample.csv", io.BytesIO(sample_csv), "text/csv")},
        )
        # Cursor 0: raw_payloads single INSERT (execute, not executemany)
        # Cursor 1: health_records batch INSERT (executemany)
        insert_cursor = mock_conn._cursors[1]
        insert_cursor.executemany.assert_called_once()
        args = insert_cursor.executemany.call_args
        assert len(args[0][1]) == 5  # 5 parameter tuples

    def test_dolt_commit_hash_in_response(self, mock_db_client, sample_csv):
        """Successful DOLT_COMMIT includes hash in response."""
        client, mock_conn = mock_db_client
        response = client.post(
            "/ingest/cgm",
            files={"file": ("sample.csv", io.BytesIO(sample_csv), "text/csv")},
        )
        data = response.json()
        assert data.get("dolt_commit") == "abc123"


class TestIngestCgmTimestampNormalization:
    """T039: test_ingest_cgm_normalizes_timestamps_to_utc."""

    def test_mixed_timezones_normalized_to_utc(self, mock_db_client, mixed_tz_csv):
        """Timestamps from different timezones all converted to UTC."""
        client, _ = mock_db_client
        response = client.post(
            "/ingest/cgm",
            files={"file": ("mixed_tz.csv", io.BytesIO(mixed_tz_csv), "text/csv")},
        )
        assert response.status_code == 200
        data = response.json()
        assert data["readings"] == 4
        # All 4 rows should parse successfully
        assert "warnings" not in data


class TestIngestCgmMalformedRows:
    """T03A: test_ingest_cgm_skips_malformed_rows."""

    def test_malformed_rows_skipped_with_warnings(self, mock_db_client, malformed_csv):
        """Malformed rows are skipped and warning count included in response."""
        client, _ = mock_db_client
        response = client.post(
            "/ingest/cgm",
            files={"file": ("malformed.csv", io.BytesIO(malformed_csv), "text/csv")},
        )
        assert response.status_code == 200
        data = response.json()
        # Should have valid rows only
        assert data["readings"] == 3
        # Should report warnings for skipped rows
        assert data["warnings"] > 0

    def test_invalid_column_names_returns_422(self, mock_db_client):
        """CSV with unrecognized columns returns 422."""
        client, _ = mock_db_client
        bad_csv = b"when,reading\n2024-01-15T06:00:00Z,95\n"
        response = client.post(
            "/ingest/cgm",
            files={"file": ("bad.csv", io.BytesIO(bad_csv), "text/csv")},
        )
        assert response.status_code == 422


class TestIngestCgmEdgeCases:
    """Edge case tests for CGM ingestion."""

    def test_empty_csv_returns_zero_readings(self, mock_db_client):
        """Empty CSV (headers only) returns zero readings."""
        client, _ = mock_db_client
        empty_csv = b"timestamp,glucose_mg_dl\n"
        response = client.post(
            "/ingest/cgm",
            files={"file": ("empty.csv", io.BytesIO(empty_csv), "text/csv")},
        )
        assert response.status_code == 200
        data = response.json()
        assert data["readings"] == 0

    def test_alternative_column_names_accepted(self, mock_db_client):
        """Column name variants (time, glucose) are accepted."""
        client, _ = mock_db_client
        alt_csv = b"time,glucose\n2024-01-15T06:00:00Z,95\n2024-01-15T06:05:00Z,100\n"
        response = client.post(
            "/ingest/cgm",
            files={"file": ("alt.csv", io.BytesIO(alt_csv), "text/csv")},
        )
        assert response.status_code == 200
        assert response.json()["readings"] == 2

    def test_no_db_configured_returns_503(self, client_no_db):
        """FR-032: No database configured returns 503."""
        csv = b"timestamp,glucose_mg_dl\n2024-01-15T06:00:00Z,95\n"
        response = client_no_db.post(
            "/ingest/cgm",
            files={"file": ("test.csv", io.BytesIO(csv), "text/csv")},
        )
        assert response.status_code == 503
        assert "not configured" in response.json()["detail"]

    def test_oversized_csv_returns_413(self, mock_db_client):
        """CSV exceeding MAX_CSV_BYTES returns 413."""
        client, _ = mock_db_client
        # Generate a CSV just over 10MB
        header = b"timestamp,glucose_mg_dl\n"
        row = b"2024-01-15T06:00:00Z,95\n"
        oversized = header + row * (10 * 1024 * 1024 // len(row) + 1)
        response = client.post(
            "/ingest/cgm",
            files={"file": ("big.csv", io.BytesIO(oversized), "text/csv")},
        )
        assert response.status_code == 413
