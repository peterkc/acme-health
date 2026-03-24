# Healthcare Compliance Standards Research

**Date**: 2026-03-21
**Status**: Complete
**Mode**: Web | **Depth**: Deep | **Workers**: 5

## Decision Summary

Healthcare compliance for an AI startup spans six regulatory domains. HIPAA is the foundation but not the ceiling. The practical differentiation for a portfolio project is **compliance by design** — where governance constraints shape architecture from day one rather than existing as policy documents.

## Key Findings

### 1. Federal Regulations (HIPAA Ecosystem)

The HIPAA ecosystem has seven major components:

| Regulation | Governing Body | Key Requirement |
|---|---|---|
| HIPAA Privacy Rule | HHS OCR | PHI use/disclosure limits, patient access rights |
| HIPAA Security Rule | HHS OCR | Administrative/physical/technical safeguards for ePHI |
| HIPAA Breach Notification | HHS OCR | 60-day notification, 4-factor risk assessment |
| HITECH Act | HHS OCR | Direct BA liability, tiered penalties up to $1.5M/yr |
| 42 CFR Part 2 | SAMHSA/HHS OCR | Extra SUD record protections (compliance by Feb 16, 2026) |
| FTC Health Breach Notification | FTC | Non-HIPAA health apps, $51,744/violation |
| ACA Section 1557 | HHS OCR/CMS | AI/algorithm non-discrimination (effective May 1, 2025) |

**Critical upcoming change**: December 2024 NPRM proposes the largest Security Rule overhaul since 2003 — annual compliance audits, 72-hour ePHI restoration, and removing the "required vs. addressable" distinction.

### 2. Security Certifications

Recommended order for healthcare AI startups:

| Phase | Certification | Timeline | Year 1 Cost | Rationale |
|---|---|---|---|---|
| 1 (0-12 mo) | SOC 2 Type II | 6-12 months | $30K-$150K | De facto required for enterprise sales |
| 2 (12-24 mo) | HITRUST i1 → r2 | 6-15 months | $40K-$200K | Healthcare gold standard; AI Security Cert available |
| 3 (24-36 mo) | ISO 27001 or FedRAMP 20x | 9-24 months | $35K-$1.5M | International or federal market expansion |
| Internal | NIST CSF 2.0 | Ongoing | $0-$50K | Governance overlay, maps to all frameworks |

**Key insight**: HITRUST r2 subsumes HIPAA + ISO 27001 + NIST 800-53. SOC 2 is the cost-efficient on-ramp (50-70% control reuse).

### 3. AI/ML-Specific Regulations

| Regulation | Status | Key Requirement |
|---|---|---|
| FDA SaMD / PCCP | Final guidance Dec 2024 | Predetermined Change Control Plans for AI updates |
| FDA CDS Four-Criteria Test | Final guidance Jan 2026 | Clinician reviewability determines device vs. non-device |
| ONC HTI-1 (Predictive DSI) | Effective Jan 2025 | FAVES framework, source attribute disclosures |
| EU AI Act (healthcare) | High-risk: Aug 2026; devices: Aug 2027 | Conformity assessment, human oversight, fines up to 7% revenue |
| CMS MA AI Guidance | Active | No AI-sole coverage denials; bias audits required |
| Colorado AI Act | Effective June 30, 2026 | Impact assessments, bias risk disclosure |
| NIST AI RMF 1.0 | Active (voluntary) | GOVERN/MAP/MEASURE/MANAGE lifecycle |

**LLM regulatory gap**: No LLM has FDA authorization. The Jan 2026 CDS guidance opens non-device pathways for LLMs meeting Criterion 4 (clinician reviewability). Dedicated LLM guidance is pending.

### 4. Data Standards & Interoperability

**Build on FHIR R4** (mandated by Cures Act). Skip R5 — R6 expected 2027+.

| Standard | Status | Portfolio Priority |
|---|---|---|
| FHIR R4 | Mandated (Cures Act, ONC) | Must-have |
| USCDI v3.1 | Required by Jan 1, 2026 | Must-have |
| SMART on FHIR v2.0 | Required for EHR API access | Must-have |
| Bulk FHIR | Required for population data | Important |
| HL7v2 | Legacy but ubiquitous (~95%) | Ingest layer |
| C-CDA | Still exchanged (500M/year) | Ingest only |
| SNOMED CT, LOINC, RxNorm | Free (UMLS license) | Must-have |
| ICD-10-CM | Free | Must-have |
| CPT | **Paid license required** (~$19.50/provider/yr) | Budget early |

### 5. State & Industry Requirements

| Requirement | Status | Impact |
|---|---|---|
| TEFCA | Live — 11 QHINs, 10,600+ orgs | Become Subparticipant for nationwide access |
| CMS-0057-F (4 APIs) | Deadline Jan 1, 2027 | Prior Auth API is highest-value opportunity |
| Washington MHMDA | Active — no size threshold | Private right of action; design to this standard |
| California CCPA/CPRA | Active | HIPAA exemption narrower than assumed |
| CARIN Blue Button | STU 2.1.0 (Feb 2025) | Claims data consumption standard |

### 6. Emerging Standards & Portfolio Relevance

**Compliance knowledge hierarchy** (what differentiates candidates):

| Tier | Examples | Signal |
|---|---|---|
| Table stakes | HIPAA, basic FHIR, BAAs | Expected, not differentiating |
| Practitioner | ONC HTI-1, FHIR Consent, SDOH Z-codes, NIST AI RMF | Shows depth |
| Strong differentiator | Bias auditing (DPD/EOR metrics), Model Cards, Synthea pipelines | Few candidates show this |
| Forward-looking | EU AI Act, ISO 42001, federated learning trade-offs | Signals strategic thinking |

### 7. Operational Compliance

| Requirement | Key Detail |
|---|---|
| BAAs | Required before any PHI disclosure; penalties $127-$1.9M/violation |
| Risk Assessment | Missing/inadequate = ~68% of HIPAA penalty settlements |
| Workforce Training | All staff, within reasonable period of hire, retain 6 years |
| Audit Logging | Log all ePHI access; 6-year minimum retention |
| Breach Notification | 60 calendar days (individuals, HHS for 500+, media for 500+ in one state) |
| Data Retention | HIPAA: 6 years (compliance docs); medical records: state law (7-30 years) |
| Incident Response | Written plan, tested annually (proposed: 72-hour restoration) |

## Portfolio Documentation Recommendations

### Compliance by Design (show in architecture, not just docs)

| Artifact | What It Demonstrates |
|---|---|
| `docs/model-card.md` | ML governance; performance transparency by demographic |
| `docs/datasheet.md` | Training data provenance and known limitations |
| `docs/ai-rmf.md` | NIST AI RMF applied to actual project decisions |
| `docs/threat-model.md` | STRIDE threat modeling for healthcare context |
| `docs/bias-audit.md` | Demographic stratification with DPD/EOR metrics |
| `docs/consent-flow.md` | Consent enforced at code level, not just policy |
| `docs/sdoh-rationale.md` | SDOH feature inclusion/exclusion with impact analysis |
| `ARCHITECTURE.md` | PHI flow diagram showing data boundaries |

### Architecture Patterns to Demonstrate

1. **Synthea data pipeline** — Professional data handling without PHI risk
2. **FHIR Consent check** — Query Consent resource before data access
3. **SDOH integration** — Z-codes in data model with performance impact docs
4. **Demographic stratification** — Every evaluation shows per-group metrics
5. **Audit trail as data model** — Typed table, not log files
6. **SMART on FHIR auth** — OAuth 2.0 + PKCE for EHR API access

## Key Dates

| Date | Event |
|---|---|
| Jan 1, 2026 | USCDI v3.1 required; TEFCA USCDI v3 exchange |
| Feb 16, 2026 | 42 CFR Part 2 full compliance deadline |
| June 30, 2026 | Colorado AI Act effective |
| Aug 2026 | EU AI Act high-risk AI fully applicable |
| Jan 1, 2027 | CMS-0057-F four APIs deadline |
| Aug 2027 | EU AI Act medical device AI fully in scope |

## Sources

Full source lists included in individual worker reports:
- `01-federal-regulations.md` — HIPAA ecosystem + operational compliance
- `02-security-certifications.md` — SOC 2, HITRUST, ISO 27001, FedRAMP, NIST
- `03-ai-ml-regulations.md` — FDA SaMD, ONC HTI-1, EU AI Act, bias laws
- `04-data-standards.md` — FHIR, USCDI, TEFCA, CMS-0057-F, state privacy
- `05-emerging-standards.md` — AI transparency, fairness, portfolio guidance
