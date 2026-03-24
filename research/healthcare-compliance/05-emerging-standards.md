# Emerging Standards & Project Relevance

**Worker**: RQ7 | **Date**: 2026-03-21

## Emerging Standards

### AI Transparency and Explainability
- FDA Jan 2025 draft guidance: TPLC requirements (document algorithm design, training data, bias mitigation, postmarket monitoring)
- FDA CDS 2026 update: Clinician reviewability determines device classification
- ONC HTI-1: Source attribute disclosures, FAVES framework
- State laws: Texas TRAIGA (Jan 1, 2026) requires written patient disclosure before AI-assisted care
- 250+ bills across 34 states by 2026 — compliance patchwork
- Key pattern: "clinician-in-the-loop" as architectural constraint

### Algorithmic Fairness Frameworks
- **HEAAL**: Health Equity Across the AI Lifecycle (delivery organization-focused)
- **CAOS**: Comprehensive Algorithmic Oversight and Stewardship (risk + equity + shadow AI)
- **SHAPEquity**: SHAP/LIME explainability integrated into compliance workflows
- **Lancet Actionable Framework**: Community-based, continuous fairness auditing
- Key metrics: DPD (Demographic Parity Difference), EOR (Equalized Odds Ratio), IFS (Intersectional Fairness Score)
- Known failure: Epic sepsis model; CNNs on public chest X-rays systematically underdiagnose Black, Hispanic, Medicaid patients

### FHIR Consent and Dynamic Consent
- FHIR Consent resource matured significantly
- 2025 SHARES Consent Engine: FHIR R5 + CDS Hooks, "possibly sensitive" categorization
- Challenge: consent reconciliation across multiple systems
- Structure: patient identity, purpose, authorized parties, data types, dates, permit/deny per category

### SDOH — Social Determinants of Health
- Gravity Project SDOH Clinical Care IG (authoritative FHIR standard)
- ICD-10-CM Z-codes (Z55-Z65) + LOINC screening instruments
- Accounts for 30-55% of health outcomes (WHO)
- Z59.0 (homelessness), Z56 (employment), Z63 (family relationships)
- Z-codes can be coded from nursing documentation (not just physician notes)

### Synthetic Data
- MITRE Synthea: standard tool for PHI-free development
- Outputs FHIR R4 Bundles, C-CDA, CSV
- 120+ disease modules (CDC/NIH statistics)
- 1M patient records freely available
- Using Synthea signals professional data handling

### Privacy-Enhancing Technologies
- Federated learning: $0.1B market (2025), 27% CAGR, only 5.2% deployed
- DP-SGD trade-off: ε ≈ 10 (clinically acceptable), ε ≈ 1 (substantial accuracy loss)
- NIST SP 800-226 (March 2025): ε ≤ 1 recommended for conservative settings
- Frameworks: NVIDIA FLARE (enterprise), Flower (research, 84.75% eval), PySyft (strongest privacy)

### SMART Health Cards
- Available to 500M+ people globally
- Signed FHIR bundles using compact FHIR / JWK
- Decentralized trust: issuers publish JWK Sets, verifiers validate signatures
- Platform support: Epic, Cerner, Oracle, CVS, Apple Wallet, Google Pay

### NIST AI RMF Documentation
- Four functions: Govern, Map, Measure, Manage
- Key artifacts: Model Cards, Datasheets for Datasets, FactSheets (IBM)
- Framework convergence: NIST AI RMF ↔ ISO/IEC 42001 ↔ EU AI Act

## Compliance Knowledge Hierarchy

| Tier | Examples | Signal |
|---|---|---|
| **Table stakes** | HIPAA, basic FHIR, BAAs, de-identification | Expected, not differentiating |
| **Practitioner** | ONC HTI-1, FHIR Consent, SDOH Z-codes, NIST AI RMF | Shows depth |
| **Strong differentiator** | Bias auditing (DPD/EOR), Model Cards, Synthea pipelines, Bulk FHIR | Few candidates show this |
| **Forward-looking** | EU AI Act Annex III, ISO 42001, federated learning trade-offs | Signals strategic thinking |

## Compliance by Design vs. Checkbox

### Checkbox compliance looks like:
- `docs/HIPAA.md` describing what HIPAA is
- PHI handling as deployment note ("encrypt in production")
- Bias section: "we are committed to fairness"
- No stratified metrics
- Consent as legal boilerplate, not code

### Compliance by design looks like:
- PHI never enters unnecessary code paths (data minimization enforced architecturally)
- Consent state checked before data access (FHIR Consent resource lookup)
- Demographic stratification in every evaluation notebook
- Model Card with actual numbers from Synthea-generated data
- Audit log as first-class data model
- SDOH features explicitly included/excluded with justification

## Recommended Documentation Artifacts

| Artifact | Demonstrates |
|---|---|
| `docs/model-card.md` | ML governance; performance transparency by demographic |
| `docs/datasheet.md` | Training data provenance and limitations |
| `docs/ai-rmf.md` | NIST AI RMF applied to actual project decisions |
| `docs/threat-model.md` | STRIDE threat modeling for healthcare context |
| `docs/bias-audit.md` | Demographic stratification with DPD/EOR metrics |
| `docs/consent-flow.md` | Consent enforced at code level |
| `docs/sdoh-rationale.md` | SDOH feature inclusion/exclusion with impact |
| `ARCHITECTURE.md` | PHI flow diagram showing data boundaries |

## Architecture Patterns to Demonstrate

1. **Synthea data pipeline** — FHIR R4 Bundles, HAPI FHIR server
2. **FHIR Consent check** — Query before any patient data access
3. **SDOH integration** — Z-codes in data model with performance impact docs
4. **Demographic stratification** — Per-group metrics in every evaluation
5. **Audit trail as data model** — Typed table with timestamp, purpose, consent status
6. **SMART on FHIR auth** — OAuth 2.0 + PKCE for EHR API access
7. **Model Card** — Committed to repo with actual numbers

## Prioritized Recommendations

### Highest impact (achievable in this project):
1. Use Synthea as data source
2. Commit a Model Card with real numbers
3. Show demographic stratification in every evaluation
4. Include FHIR Consent check in data access layer
5. Include SDOH Z-codes with impact documentation

### High impact, moderate effort:
6. NIST AI RMF alignment document mapped to project decisions
7. Audit log as typed data model
8. Architecture diagram showing PHI flow and boundaries

### Strong forward-looking signal:
9. SMART Health Card generation for patient export
10. Differential privacy demo with explicit ε and trade-off docs

## Sources
- FDA.gov, ONC/ASTP, NIST AI RMF
- Nature npj Digital Medicine, The Lancet Digital Health
- MITRE Synthea, VCI (SMART Health Cards)
- Gravity Project, HL7 SDOH Clinical Care IG
