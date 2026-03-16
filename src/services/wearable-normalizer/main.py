import fastapi

app = fastapi.FastAPI(title="Wearable Normalizer")


@app.get("/health")
async def health():
    return {"status": "healthy"}


@app.post("/ingest/cgm")
async def ingest_cgm():
    """Ingest and normalize continuous glucose monitor readings."""
    return {"status": "not_implemented"}


@app.post("/ingest/activity")
async def ingest_activity():
    """Ingest and normalize activity data (heart rate, steps, sleep)."""
    return {"status": "not_implemented"}
