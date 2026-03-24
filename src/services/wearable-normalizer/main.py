"""Wearable Normalizer — CGM CSV ingestion with Dolt MySQL persistence."""

import hashlib
import io
import logging
import os
import uuid
from contextlib import asynccontextmanager
from datetime import UTC, datetime
from decimal import Decimal

import aiomysql
import fastapi
import pandas as pd
from fastapi import HTTPException, UploadFile

# 10 MB — sufficient for months of 5-minute interval CGM data
MAX_CSV_BYTES = 10 * 1024 * 1024

logger = logging.getLogger("wearable-normalizer")

# SQL schema constants — three-layer design matching Acme.Stack.Core.Schema (ADR-2005)
# Layer 1: Canonical tables (query-optimized)
CREATE_PATIENTS = """
CREATE TABLE IF NOT EXISTS patients (
    id VARCHAR(255) PRIMARY KEY,
    family_name VARCHAR(255),
    given_name VARCHAR(255),
    birth_date DATE,
    gender VARCHAR(50),
    source_standard VARCHAR(50) DEFAULT 'fhir-r4',
    source_version VARCHAR(50) DEFAULT 'R4/4.0.1',
    extensions JSON,
    ingested_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);
"""

CREATE_HEALTH_RECORDS = """
CREATE TABLE IF NOT EXISTS health_records (
    id VARCHAR(255) PRIMARY KEY,
    patient_id VARCHAR(255),
    record_type VARCHAR(100) NOT NULL,
    code VARCHAR(255),
    code_system VARCHAR(255),
    display VARCHAR(500),
    value_numeric DECIMAL(18,4),
    value_text TEXT,
    unit VARCHAR(100),
    device_name VARCHAR(255),
    device_type VARCHAR(100),
    effective_date DATETIME,
    source_standard VARCHAR(50) NOT NULL,
    source_version VARCHAR(50),
    extensions JSON,
    ingested_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT fk_hr_patient FOREIGN KEY (patient_id) REFERENCES patients(id)
);
"""

CREATE_CLINICAL_ENTITIES = """
CREATE TABLE IF NOT EXISTS clinical_entities (
    id VARCHAR(255) PRIMARY KEY,
    patient_id VARCHAR(255),
    entity_type VARCHAR(100) NOT NULL,
    code VARCHAR(255),
    code_system VARCHAR(255),
    display VARCHAR(500),
    confidence DECIMAL(5,4),
    needs_review BOOLEAN DEFAULT FALSE,
    source_text_span TEXT,
    model_id VARCHAR(255),
    source_standard VARCHAR(50),
    extensions JSON,
    ingested_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT fk_ce_patient FOREIGN KEY (patient_id) REFERENCES patients(id)
);
"""

# Layer 2: Raw payload archive (re-parseable)
CREATE_RAW_PAYLOADS = """
CREATE TABLE IF NOT EXISTS raw_payloads (
    id VARCHAR(255) PRIMARY KEY,
    content_type VARCHAR(100) NOT NULL,
    source_standard VARCHAR(50) NOT NULL,
    source_version VARCHAR(50),
    payload LONGTEXT NOT NULL,
    payload_hash VARCHAR(64) NOT NULL,
    ingested_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    UNIQUE KEY uq_payload_hash (payload_hash)
);
"""

# Layer 3: Provenance chain (lineage)
CREATE_PROVENANCE = """
CREATE TABLE IF NOT EXISTS provenance (
    id VARCHAR(255) PRIMARY KEY,
    target_table VARCHAR(100) NOT NULL,
    target_id VARCHAR(255) NOT NULL,
    raw_payload_id VARCHAR(255) NOT NULL,
    transform VARCHAR(100) NOT NULL,
    transform_version VARCHAR(50),
    dolt_commit VARCHAR(64),
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT fk_prov_raw FOREIGN KEY (raw_payload_id) REFERENCES raw_payloads(id)
);
"""

# All tables in dependency order (foreign keys satisfied by creation order)
ALL_TABLES = [
    CREATE_PATIENTS,
    CREATE_HEALTH_RECORDS,
    CREATE_CLINICAL_ENTITIES,
    CREATE_RAW_PAYLOADS,
    CREATE_PROVENANCE,
]

# Column name variants — ordered by preference, first match wins
TIMESTAMP_COLUMNS = ("timestamp", "time", "datetime", "date_time", "timestamp_utc")
GLUCOSE_COLUMNS = ("glucose_mg_dl", "glucose", "value", "glucose_value", "mg_dl")


def parse_connection_string(dotnet_conn_str: str) -> dict:
    """Convert .NET MySQL connection string to aiomysql kwargs.

    Input:  Server=localhost;Port=3306;User ID=root;Password=pw;Database=acme_health
    Output: {"host": "localhost", "port": 3306, "user": "root",
             "password": "pw", "db": "acme_health"}
    """
    key_map = {
        "server": "host",
        "host": "host",
        "port": "port",
        "user id": "user",
        "user": "user",
        "username": "user",
        "password": "password",
        "database": "db",
        "initial catalog": "db",
    }
    result = {}
    for pair in dotnet_conn_str.split(";"):
        pair = pair.strip()
        if not pair or "=" not in pair:
            continue
        key, value = pair.split("=", 1)
        mapped = key_map.get(key.strip().lower())
        if mapped:
            result[mapped] = int(value.strip()) if mapped == "port" else value.strip()

    missing = {"host", "db"} - set(result.keys())
    if missing:
        raise ValueError(
            f"Connection string missing required keys ({', '.join(sorted(missing))}): "
            f"{dotnet_conn_str}"
        )
    return result


def get_connection_string() -> dict | None:
    """Read and convert the Aspire-injected connection string."""
    raw = os.environ.get("ConnectionStrings__acme-health")
    if not raw:
        return None
    return parse_connection_string(raw)


async def create_schema(conn_kwargs: dict) -> None:
    """Create all tables on startup in dependency order (idempotent).

    Mirrors Schema.AllTables from Acme.Stack.Core (ADR-2005).
    Raises on failure so the lifespan can decide whether to proceed without DB.
    """
    conn = None
    try:
        conn = await aiomysql.connect(**conn_kwargs)
        async with conn.cursor() as cur:
            for ddl in ALL_TABLES:
                await cur.execute(ddl)
        await conn.commit()
        logger.info("Database schema initialized (%d tables)", len(ALL_TABLES))
    except Exception:
        logger.exception("Failed to initialize database schema")
        raise
    finally:
        if conn:
            conn.close()


@asynccontextmanager
async def lifespan(application: fastapi.FastAPI):
    """Run startup tasks: schema creation."""
    conninfo = get_connection_string()
    if conninfo:
        try:
            await create_schema(conninfo)
            application.state.conninfo = conninfo
        except Exception:
            logger.warning(
                "DB unavailable at startup — ingest endpoints will return 503"
            )
            application.state.conninfo = None
    else:
        logger.warning("No ConnectionStrings__acme-health — DB features disabled")
        application.state.conninfo = None
    yield


app = fastapi.FastAPI(title="Wearable Normalizer", lifespan=lifespan)


@app.get("/health")
async def health():
    return {"status": "healthy"}


def _find_column(df_columns: list[str], candidates: tuple[str, ...]) -> str | None:
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
            f"No timestamp column found. Expected one of: {list(TIMESTAMP_COLUMNS)}"
        )
    if gl_col is None:
        raise ValueError(
            f"No glucose column found. Expected one of: {list(GLUCOSE_COLUMNS)}"
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
    FR-022: Persist to health_records + raw_payloads + provenance (ADR-2005).
    FR-023: Return summary with reading count and time range.
    FR-031: Skip malformed rows, include warning count.
    """
    conninfo: dict | None = getattr(app.state, "conninfo", None)

    # FR-032: Database must be configured (consistent with C# FHIR ingest)
    if conninfo is None:
        raise HTTPException(status_code=503, detail="Database is not configured")

    # Read CSV content in chunks to enforce size limit before full buffering
    content = bytearray()
    chunk_size = 64 * 1024  # 64 KB
    while chunk := await file.read(chunk_size):
        content.extend(chunk)
        if len(content) > MAX_CSV_BYTES:
            raise HTTPException(
                status_code=413,
                detail=f"CSV too large (max {MAX_CSV_BYTES} bytes)",
            )
    filename = file.filename or "unknown.csv"
    content = bytes(content)

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

    # Persist to Dolt MySQL (FR-022) — three-layer write (ADR-2005)
    try:
        conn = await aiomysql.connect(**conninfo)
        try:
            # --- Layer 2: Archive raw CSV payload ---
            raw_payload_id = str(uuid.uuid4())
            csv_text = content.decode("utf-8", errors="replace")
            payload_hash = hashlib.sha256(content).hexdigest()

            async with conn.cursor() as cur:
                await cur.execute(
                    "INSERT IGNORE INTO raw_payloads"
                    " (id, content_type, source_standard, payload, payload_hash)"
                    " VALUES (%s, %s, %s, %s, %s)",
                    (raw_payload_id, "text/csv", "csv/cgm", csv_text, payload_hash),
                )

            # --- Layer 1: Insert health_records (batch) ---
            record_ids: list[str] = []
            async with conn.cursor() as cur:
                params = []
                for _, row in df.iterrows():
                    record_id = str(uuid.uuid4())
                    record_ids.append(record_id)
                    params.append((
                        record_id,
                        "cgm",
                        "glucose-mg-dl",
                        "custom/cgm",
                        "Glucose (mg/dL)",
                        Decimal(str(row["glucose_mg_dl"])),
                        "mg/dL",
                        "cgm",
                        row["timestamp_utc"].to_pydatetime(),
                        "csv/cgm",
                    ))
                await cur.executemany(
                    "INSERT INTO health_records"
                    " (id, record_type, code, code_system, display,"
                    "  value_numeric, unit, device_type, effective_date,"
                    "  source_standard)"
                    " VALUES (%s, %s, %s, %s, %s, %s, %s, %s, %s, %s)",
                    params,
                )

            # --- Layer 3: Provenance (link each health_record to raw_payload) ---
            async with conn.cursor() as cur:
                prov_params = [
                    (
                        str(uuid.uuid4()),
                        "health_records",
                        rid,
                        raw_payload_id,
                        "csv-cgm-normalizer",
                    )
                    for rid in record_ids
                ]
                await cur.executemany(
                    "INSERT INTO provenance"
                    " (id, target_table, target_id, raw_payload_id, transform)"
                    " VALUES (%s, %s, %s, %s, %s)",
                    prov_params,
                )

            await conn.commit()

            # Dolt MySQL version tracking — best-effort
            dolt_commit_hash = None
            versioning_warning = None
            try:
                async with conn.cursor() as cur:
                    timestamp = datetime.now(UTC).isoformat()
                    await cur.execute(
                        "CALL DOLT_COMMIT('-Am', %s)",
                        (f"Ingest: CGM {filename} at {timestamp}",),
                    )
                    row_result = await cur.fetchone()
                    dolt_commit_hash = row_result[0] if row_result else None
                    logger.info("Dolt commit: %s", dolt_commit_hash)
            except Exception as exc:
                logger.warning("DOLT_COMMIT failed: %s", exc)
                versioning_warning = f"DOLT_COMMIT failed: {exc}"

            # Backfill dolt_commit into provenance records for this ingest
            if dolt_commit_hash:
                try:
                    async with conn.cursor() as cur:
                        await cur.execute(
                            "UPDATE provenance SET dolt_commit = %s"
                            " WHERE raw_payload_id = %s AND dolt_commit IS NULL",
                            (dolt_commit_hash, raw_payload_id),
                        )
                    await conn.commit()
                except Exception as exc:
                    logger.warning(
                        "Failed to backfill dolt_commit: %s", exc
                    )
        finally:
            conn.close()

    except Exception as exc:
        # FR-032: Dolt MySQL unreachable returns HTTP 503
        logger.error("Dolt MySQL unreachable: %s", exc)
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
