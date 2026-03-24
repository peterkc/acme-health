# Consent Architecture

> Design for FHIR Consent enforcement in the ACME Health platform.
> Not yet implemented — this document describes the planned architecture.

## Current State

No consent enforcement exists. All API endpoints accept unauthenticated requests. Any client with network access can submit data to or query data from the platform.

This is acceptable for a demo project with Synthea synthetic data. Production deployment requires consent enforcement before any endpoint processes real patient data.

## FHIR Consent Resource

The FHIR R4 [Consent](https://hl7.org/fhir/R4/consent.html) resource captures a patient's agreement or refusal for specific data uses. A single Consent resource encodes:

| Field | Purpose |
|-------|---------|
| `patient` | Which patient this consent applies to |
| `scope` | Category: patient-privacy, research, treatment |
| `category` | Specific consent type (e.g., HIPAA Authorization) |
| `dateTime` | When consent was recorded |
| `provision.type` | `permit` or `deny` |
| `provision.actor` | Who is authorized (or denied) |
| `provision.action` | What actions are authorized |
| `provision.purpose` | Purpose codes (treatment, operations, research) |
| `provision.class` | Data types covered (Patient, Observation, etc.) |

A patient might permit access to treatment data but deny AI processing of their clinical notes. The Consent resource supports this granularity through nested provisions.

## Enforcement Points

Three services access patient data. Each needs a consent check before processing:

### Data API (`GET /patients`, `GET /observations`)

The most straightforward enforcement point. Before returning patient data, the API queries the Consent resource for the requesting user's authorization and the requested purpose.

```
Client request → Authenticate (SMART on FHIR) → Check Consent → Return data or 403
```

### Clinical Extractor (`POST /extract`)

Consent for AI processing is separate from consent for treatment data access. A patient may consent to their doctor viewing lab results but not to an LLM processing their clinical notes.

[ADR-2004](../adr/2004-human-in-the-loop-clinical-ai.md) mandates human review of AI extractions. The consent check adds another gate: even if the extraction produces high-confidence results, the system should verify that the patient consented to AI processing before the extraction runs.

```
Clinical note → Check AI processing consent → Extract entities → HITL review → Commit
```

### Wearable Normalizer (`POST /ingest/cgm`)

CGM data consent may differ from clinical data consent. Wearable data originates from consumer devices, not clinical encounters, and patients may have different expectations about how this data is used.

```
CSV upload → Check wearable data consent → Normalize → Persist
```

## Consent Model

### Granular Purpose Codes

| Purpose | Scope | Example |
|---------|-------|---------|
| `TREAT` | Treatment | Clinician views patient record |
| `HPAYMT` | Payment | Claims processing access |
| `HRESCH` | Research | De-identified data for studies |
| `CLINTRCH` | Clinical research | Identified data for clinical trials |
| `AI_PROC` | AI processing (custom) | LLM extraction of clinical entities |

The `AI_PROC` purpose code does not exist in the FHIR standard vocabulary. It would be defined as a local code system extension. The distinction matters: a patient who consents to treatment access has not implicitly consented to AI processing of their notes.

### Consent Lifecycle

```
Patient creates consent → Stored in Dolt MySQL → Checked on each data access
                      ↓
              Patient revokes consent → Consent updated (status: inactive)
                      ↓
              Subsequent requests denied for that purpose
```

Consent changes are Dolt-committed like any other data mutation, creating an audit trail of consent state over time. The `dolt_diff()` query can answer: "Was this patient's consent active at the time this extraction ran?"

## 42 CFR Part 2 — Substance Use Disorder Records

42 CFR Part 2 imposes stricter consent requirements for substance use disorder (SUD) treatment records than standard HIPAA. The 2024 Final Rule (compliance deadline February 16, 2026) aligns enforcement with HIPAA but maintains the higher consent bar.

Key differences from standard HIPAA consent:

| Requirement | HIPAA | 42 CFR Part 2 |
|-------------|-------|---------------|
| Disclosure without consent | Permitted for treatment, payment, operations | Generally prohibited |
| Consent revocation | Must include mechanism | Consent form must describe how to revoke |
| Legal proceedings | Subpoena sufficient | Court order AND subpoena required, plus patient consent |
| Breach notification | Required | Now required (aligned with HIPAA as of 2024) |
| Re-disclosure | No special restrictions | Must include statement: "42 CFR Part 2 prohibits unauthorized use or disclosure of these records" |

For the ACME Health platform, this means: any clinical note that mentions SUD treatment requires a separate, more restrictive consent check. The Consent resource can model this through a provision with `class` restricted to SUD-related Observation and Condition resources.

See [`research/healthcare-compliance/01-federal-regulations.md`](../../research/healthcare-compliance/01-federal-regulations.md) for the full regulatory analysis.

## Implementation Path

### Phase 1: Schema

Add a `consents` table to Dolt MySQL alongside `patients` and `observations`:

```sql
CREATE TABLE IF NOT EXISTS consents (
    id VARCHAR(255) PRIMARY KEY,
    patient_id VARCHAR(255) NOT NULL,
    scope VARCHAR(50) NOT NULL,
    status VARCHAR(20) NOT NULL,  -- active, inactive, rejected
    purpose VARCHAR(50) NOT NULL, -- TREAT, HRESCH, AI_PROC, etc.
    provision_type VARCHAR(10) NOT NULL, -- permit, deny
    date_time TIMESTAMP NOT NULL,
    ingested_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT fk_consent_patient FOREIGN KEY (patient_id) REFERENCES patients(id)
);
```

### Phase 2: Middleware

Add consent-checking middleware to the Data API and Python services. The check runs after authentication (SMART on FHIR) and before data access:

1. Extract patient ID and purpose from the request context
2. Query the `consents` table for an active `permit` provision matching the patient, purpose, and requesting actor
3. If no matching consent exists, return HTTP 403 with a structured error indicating the missing consent
4. If consent is `deny`, return HTTP 403

### Phase 3: Consent Management API

Add endpoints for creating, updating, and revoking consent records. These endpoints themselves require authentication and should be accessible to the patient or their authorized representative.

### Phase 4: 42 CFR Part 2 Data Segmentation

Tag SUD-related resources with a security label. The consent middleware checks for the SUD label and applies the stricter Part 2 consent requirements.

## Dependencies

Consent enforcement depends on authentication. Without knowing *who* is requesting data, consent checks cannot determine *whether* that requestor has the patient's consent. SMART on FHIR authentication is the prerequisite.

The implementation sequence: authentication → consent schema → consent middleware → consent management API → Part 2 segmentation.
