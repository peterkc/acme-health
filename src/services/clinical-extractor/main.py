import fastapi

app = fastapi.FastAPI(title="Clinical Extractor")


@app.get("/health")
async def health():
    return {"status": "healthy"}


@app.post("/extract")
async def extract_entities():
    """Extract structured medical entities from clinical note text.

    Returns medications, diagnoses (ICD-10), procedures (CPT),
    and lab values (LOINC) with confidence scores.
    """
    return {"status": "not_implemented"}


@app.post("/review")
async def submit_review():
    """Submit human review decision for low-confidence extractions."""
    return {"status": "not_implemented"}
