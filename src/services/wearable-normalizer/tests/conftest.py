"""Shared fixtures for wearable-normalizer tests."""

import os
from pathlib import Path
from unittest.mock import AsyncMock, patch

import pytest
from fastapi.testclient import TestClient

from main import app

FIXTURES_DIR = Path(__file__).parent / "fixtures"

# Fake connection kwargs used during tests — lifespan parses env var into this
_FAKE_CONNINFO = {
    "host": "localhost",
    "port": 3306,
    "user": "root",
    "password": "doltpass",
    "db": "acme_health",
}


@pytest.fixture()
def client():
    """FastAPI test client with a fake conninfo (schema creation mocked)."""
    fake_conn_str = (
        "Server=localhost;Port=3306;User ID=root;Password=doltpass;Database=acme_health"
    )
    with (
        patch.dict(os.environ, {"ConnectionStrings__acme-health": fake_conn_str}),
        patch("main.create_schema", new_callable=AsyncMock),
    ):
        with TestClient(app) as c:
            yield c


@pytest.fixture()
def client_no_db():
    """FastAPI test client with no DB connection (conninfo=None)."""
    # Ensure no env var so lifespan sets conninfo=None
    env = os.environ.copy()
    env.pop("ConnectionStrings__acme-health", None)
    with patch.dict(os.environ, env, clear=True):
        with TestClient(app) as c:
            yield c


@pytest.fixture()
def sample_csv() -> bytes:
    """Load the sample CGM CSV fixture."""
    return (FIXTURES_DIR / "sample.csv").read_bytes()


@pytest.fixture()
def malformed_csv() -> bytes:
    """Load the malformed CGM CSV fixture."""
    return (FIXTURES_DIR / "malformed.csv").read_bytes()


@pytest.fixture()
def mixed_tz_csv() -> bytes:
    """Load the mixed-timezone CGM CSV fixture."""
    return (FIXTURES_DIR / "mixed_tz.csv").read_bytes()
