# Data Standards & Interoperability + State/Industry Requirements

**Worker**: RQ4 + RQ5 | **Date**: 2026-03-21

## Data Standards

### FHIR — Build on R4, Skip R5
- R4 (4.0.1) mandated by 21st Century Cures Act
- R5: trial-use, limited adoption — skip it
- R6: normative ballot Jan 2026, final release ≥2027
- Strategy: Build on R4, plan direct migration to R6

### USCDI — v3.1 Is the Compliance Floor
| Version | Status |
|---------|--------|
| v3.1 | **Required January 1, 2026** (HTI-1) |
| v4-v6 | Voluntary via SVAP |
| v6 | Published July 2025 (adds Care Plan, UDI, Portable Medical Order) |
| Draft v7 | Published January 2026, in public comment |

Also required for TEFCA exchange as of January 1, 2026.

### HL7v2
- Present in ~95% of US healthcare organizations
- Not mandated for new systems but de facto required for ingesting hospital data
- Build an ingest layer; use FHIR as output standard

### C-CDA
- 500M+ documents exchanged annually
- Build ingest pipeline; use C-CDA-on-FHIR as bridge
- Do not originate C-CDA for new workflows

### SMART on FHIR v2.0
- Required for ONC G10 certification (deadline Dec 31, 2025)
- OAuth 2.0 + OpenID Connect + PKCE
- Two patterns: EHR Launch, Standalone Launch
- Server-to-server: SMART Backend Services (JWKS)

### Bulk FHIR
- Asynchronous export in NDJSON format
- Required for ONC §170.315(g)(10) certification
- CMS-0057-F Provider Access API requires bulk access
- Operations: `$export` at system/group/patient level

### Terminology Standards

| Code System | License | Cost | USCDI Required | Notes |
|---|---|---|---|---|
| ICD-10-CM | None | Free | Yes (diagnoses) | Annual Oct 1 update |
| SNOMED CT | UMLS | Free (US) | Yes (problems) | Register at NLM UTS |
| LOINC | Free commercial | Free | Yes (labs, vitals) | Most startup-friendly |
| RxNorm | UMLS | Free | Yes (medications) | Weekly updates |
| CPT | **Paid AMA license** | ~$19.50/provider/yr | No (billing) | Budget this early |
| NDC | None | Free | No | FDA Directory |
| ICD-11 | None | Free | No US mandate | Architect for future |

## State & Industry Requirements

### TEFCA (Trusted Exchange Framework)
- Live with 11 QHINs, 10,600+ participating organizations (Nov 2025)
- Become a Subparticipant (via Health Gorilla, eHealth Exchange) — not QHIN
- USCDI v3 required for all exchange as of Jan 1, 2026
- Highest-leverage path to nationwide patient record access

### CMS-0057-F — Four Required APIs

| API | Function | Deadline |
|---|---|---|
| Patient Access API | Claims + clinical data to members | Jan 1, 2027 |
| Provider Access API | Member data to in-network providers | Jan 1, 2027 |
| Provider Directory API | Public directory, 30-day update SLA | Jan 1, 2027 |
| Prior Authorization API | Electronic PA check + request/response | Jan 1, 2027 |

- PA decisions: 72 hours (urgent) / 7 days (standard) — already in effect
- PA API is FHIR-only; CMS granted HIPAA enforcement discretion removing X12 278
- **Highest-value integration opportunity** in US healthcare

### CARIN Blue Button
- STU 2.1.0 (February 2025)
- FHIR ExplanationOfBenefit profiles for adjudicated claims
- Now includes dental and vision profiles
- CMS Blue Button 2.0 covers 53M+ Medicare FFS beneficiaries

### State Privacy Laws

#### California CCPA/CPRA
- HIPAA exemption covers PHI held by covered entities only
- NOT exempt: employee data, web analytics, non-CE health tech data
- Thresholds: >$25M revenue, OR 100K+ CA residents, OR 50%+ revenue from data

#### Washington My Health My Data Act (MHMDA)
- **No minimum size threshold** (unlike CCPA)
- Covers health data derived/inferred from non-health data
- **Private right of action** (per se CPA violation)
- Geofencing ban around healthcare facilities
- Design to this standard as the highest bar

### HIEs and Payer-to-Payer Exchange
- HIE participation: state-by-state (some mandate, e.g., New York)
- Payer-to-Payer API (CMS-0057-F): up to 5 years of member history on plan switch
- Deadline: January 1, 2027

## Key Decisions for acme-health

1. Build on FHIR R4 + US Core IG
2. Target USCDI v3.1 compliance
3. Implement SMART on FHIR v2.0
4. Budget for CPT licensing
5. Become a TEFCA Subparticipant (design decision)
6. Prior Authorization API as highest-value integration
7. Design privacy to Washington MHMDA standard (highest bar)

## Sources
- ONC ISP (USCDI), HL7.org, HealthIT.gov (TEFCA)
- CMS.gov (CMS-0057-F)
- Health Samurai, Firely, Metriport (FHIR analysis)
- Washington RCW 19.373, California CCPA
