"""Wearable Normalizer — CGM CSV ingestion with DoltgreSQL persistence."""

import io
import logging
import os
from contextlib import asynccontextmanager
from datetime import UTC, datetime
from decimal import Decimal

import fastapi
import pandas as pd
import psycopg
from fastapi import HTTPException, UploadFile

# 10 MB — sufficient for months of 5-minute interval CGM data
MAX_CSV_BYTES = 10 * 1024 * 1024

logger = logging.getLogger("wearable-normalizer")

# SQL: cgm_readings table schema (idempotent via IF NOT EXISTS)
CREATE_CGM_READINGS = """
CREATE TABLE IF NOT EXISTS cgm_readings (
    id SERIAL PRIMARY KEY,
    timestamp_utc TIMESTAMP NOT NULL,
    glucose_mg_dl DECIMAL NOT NULL,
    source_file TEXT,
    ingested_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);
"""

# Column name variants for timestamp and glucose value
TIMESTAMP_COLUMNS = {"timestamp", "time", "datetime", "date_time", "timestamp_utc"}
GLUCOSE_COLUMNS = {"glucose_mg_dl", "glucose", "value", "glucose_value", "mg_dl"}


def parse_connection_string(dotnet_conn_str: str) -> str:
    """Convert .NET connection string to psycopg DSN format.

    .NET format: Host=localhost;Port=5432;Username=root;Database=acme_health
    psycopg format: host=localhost port=5432 user=root dbname=acme_health
    """
    key_map = {
        "host": "host",
        "port": "port",
        "username": "user",
        "user": "user",
        "database": "dbname",
        "password": "password",
    }
    parts = []
    for pair in dotnet_conn_str.split(";"):
        pair = pair.strip()
        if not pair:
            continue
        if "=" not in pair:
            continue
        key, value = pair.split("=", 1)
        pg_key = key_map.get(key.strip().lower())
        if pg_key:
            parts.append(f"{pg_key}={value.strip()}")
    return " ".join(parts)


def get_connection_string() -> str | None:
    """Read and convert the Aspire-injected connection string."""
    raw = os.environ.get("ConnectionStrings__doltgresql")
    if not raw:
        return None
    return parse_connection_string(raw)


async def create_schema(conninfo: str) -> None:
    """Create cgm_readings table on startup (idempotent)."""
    try:
        async with await psycopg.AsyncConnection.connect(conninfo) as conn:
            await conn.execute(CREATE_CGM_READINGS)
            await conn.commit()
            logger.info("Database schema initialized (cgm_readings)")
    except psycopg.Error as exc:
        logger.warning("Failed to initialize database schema: %s", exc)


@asynccontextmanager
async def lifespan(application: fastapi.FastAPI):
    """Run startup tasks: schema creation."""
    conninfo = get_connection_string()
    if conninfo:
        await create_schema(conninfo)
        application.state.conninfo = conninfo
    else:
        logger.warning("No ConnectionStrings__doltgresql — DB features disabled")
        application.state.conninfo = None
    yield


app = fastapi.FastAPI(title="Wearable Normalizer", lifespan=lifespan)


@app.get("/health")
async def health():
    return {"status": "healthy"}


def _find_column(df_columns: list[str], candidates: set[str]) -> str | None:
    """Find the first matching column name (case-insensitive)."""
    lower_map = {c.lower().strip(): c for c in df_columns}
    for candidate in candidates:
        if candidate in lower_map:
            return lower_map[candidate]
    return None


def parse_cgm_csv(content: bytes, filename: str) -> tuple[pd.DataFrame, int]:
    """Parse CGM CSV content, returning (valid_df, warnings_count).

    Handles column name variants and malformed rows.
    Returns a DataFrame with normalized columns: timestamp_utc, glucose_mg_dl.
    """
    df = pd.read_csv(io.BytesIO(content))

    if df.empty:
        empty_cols = ["timestamp_utc", "glucose_mg_dl", "source_file"]
        return pd.DataFrame(columns=empty_cols), 0

    # Resolve column names (case-insensitive matching)
    ts_col = _find_column(list(df.columns), TIMESTAMP_COLUMNS)
    gl_col = _find_column(list(df.columns), GLUCOSE_COLUMNS)

    if ts_col is None:
        raise ValueError(
            f"No timestamp column found. Expected one of: {sorted(TIMESTAMP_COLUMNS)}"
        )
    if gl_col is None:
        raise ValueError(
            f"No glucose column found. Expected one of: {sorted(GLUCOSE_COLUMNS)}"
        )

    total_rows = len(df)
    warnings = 0

    # Normalize timestamps to UTC ISO 8601 (format='mixed' handles
    # varying formats: ISO with T, space-separated, with/without TZ offset)
    df["timestamp_utc"] = pd.to_datetime(
        df[ts_col], errors="coerce", utc=True, format="mixed"
    )
    ts_bad = df["timestamp_utc"].isna()
    warnings += int(ts_bad.sum())

    # Normalize glucose values to Decimal
    df["glucose_mg_dl"] = pd.to_numeric(df[gl_col], errors="coerce")
    gl_bad = df["glucose_mg_dl"].isna()
    warnings += int(gl_bad.sum()) - int((ts_bad & gl_bad).sum())  # avoid double-count

    # Drop rows where either column is bad (FR-031: skip malformed rows)
    bad_mask = ts_bad | gl_bad
    df = df[~bad_mask].copy()
    df["source_file"] = filename

    logger.info(
        "Parsed %d/%d rows from %s (%d warnings)",
        len(df),
        total_rows,
        filename,
        warnings,
    )

    return df[["timestamp_utc", "glucose_mg_dl", "source_file"]], warnings


@app.post("/ingest/cgm")
async def ingest_cgm(file: UploadFile) -> dict:
    """Ingest and normalize continuous glucose monitor readings.

    FR-020: Parse glucose readings (timestamp, glucose_mg_dl) from CSV.
    FR-021: Normalize timestamps to UTC ISO 8601.
    FR-022: Persist to cgm_readings table via psycopg.
    FR-023: Return summary with reading count and time range.
    FR-031: Skip malformed rows, include warning count.
    """
    conninfo: str | None = getattr(app.state, "conninfo", None)

    # FR-032: Database must be configured (consistent with C# FHIR ingest)
    if conninfo is None:
        raise HTTPException(status_code=503, detail="Database is not configured")

    # Read CSV content with size limit
    content = await file.read()
    if len(content) > MAX_CSV_BYTES:
        raise HTTPException(
            status_code=413,
            detail=f"CSV too large ({len(content)} bytes, max {MAX_CSV_BYTES})",
        )
    filename = file.filename or "unknown.csv"

    # Parse and normalize CSV
    try:
        df, warnings = parse_cgm_csv(content, filename)
    except ValueError as exc:
        raise HTTPException(status_code=422, detail=str(exc)) from exc
    except Exception as exc:
        logger.error("CSV parsing failed: %s", exc)
        raise HTTPException(
            status_code=422, detail=f"Failed to parse CSV: {exc}"
        ) from exc

    if df.empty:
        return {"readings": 0, "warnings": warnings}

    # Build response summary (FR-023)
    readings_count = len(df)
    start_ts = df["timestamp_utc"].min().isoformat()
    end_ts = df["timestamp_utc"].max().isoformat()

    # Persist to DoltgreSQL (FR-022) — batch insert for NFR-002 performance
    try:
        async with await psycopg.AsyncConnection.connect(conninfo) as conn:
            async with conn.cursor() as cur:
                # Build parameter tuples for executemany (batch insert)
                params = [
                    (
                        row["timestamp_utc"].to_pydatetime(),
                        Decimal(str(row["glucose_mg_dl"])),
                        row["source_file"],
                    )
                    for _, row in df.iterrows()
                ]
                await cur.executemany(
                    "INSERT INTO cgm_readings"
                    " (timestamp_utc, glucose_mg_dl, source_file)"
                    " VALUES (%s, %s, %s)",
                    params,
                )
            await conn.commit()

            # DoltgreSQL version tracking — best-effort
            dolt_commit_hash = None
            versioning_warning = None
            try:
                async with conn.cursor() as cur:
                    timestamp = datetime.now(UTC).isoformat()
                    await cur.execute(
                        "SELECT DOLT_COMMIT('-Am', %s)",
                        (f"Ingest: CGM {filename} at {timestamp}",),
                    )
                    row_result = await cur.fetchone()
                    dolt_commit_hash = row_result[0] if row_result else None
                    logger.info("DoltgreSQL commit: %s", dolt_commit_hash)
            except Exception as exc:
                # Log but don't fail — data is persisted, versioning is best-effort
                logger.warning("DOLT_COMMIT failed: %s", exc)
                versioning_warning = f"DOLT_COMMIT failed: {exc}"

    except psycopg.Error as exc:
        # FR-032: DoltgreSQL unreachable returns HTTP 503
        logger.error("DoltgreSQL unreachable: %s", exc)
        raise HTTPException(status_code=503, detail="Database is unavailable") from exc

    result: dict = {
        "readings": readings_count,
        "start": start_ts,
        "end": end_ts,
    }
    if dolt_commit_hash:
        result["dolt_commit"] = dolt_commit_hash
    if versioning_warning:
        result["versioning_warning"] = versioning_warning
    if warnings > 0:
        result["warnings"] = warnings
    return result


@app.post("/ingest/activity")
async def ingest_activity():
    """Ingest and normalize activity data (heart rate, steps, sleep)."""
    return {"status": "not_implemented"}
