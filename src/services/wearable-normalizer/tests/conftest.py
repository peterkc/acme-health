"""Shared fixtures for wearable-normalizer tests."""

import os
from pathlib import Path
from unittest.mock import AsyncMock, patch

import pytest
from fastapi.testclient import TestClient

from main import app

FIXTURES_DIR = Path(__file__).parent / "fixtures"

# Fake connection string used during tests — lifespan reads this env var
_FAKE_CONNINFO = "host=localhost port=5432 user=root dbname=acme_health"


@pytest.fixture()
def client():
    """FastAPI test client with a fake conninfo (schema creation mocked)."""
    fake_conn_str = "Host=localhost;Port=5432;Username=root;Database=acme_health"
    with (
        patch.dict(os.environ, {"ConnectionStrings__doltgresql": fake_conn_str}),
        patch("main.create_schema", new_callable=AsyncMock),
    ):
        with TestClient(app) as c:
            yield c


@pytest.fixture()
def client_no_db():
    """FastAPI test client with no DB connection (conninfo=None)."""
    # Ensure no env var so lifespan sets conninfo=None
    env = os.environ.copy()
    env.pop("ConnectionStrings__doltgresql", None)
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
