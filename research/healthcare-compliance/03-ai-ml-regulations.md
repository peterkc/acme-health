# AI/ML Healthcare Regulations

**Worker**: RQ3 | **Date**: 2026-03-21

## Regulation Matrix

| Regulation | Governing Body | Status | Primary AI/ML Requirement |
|---|---|---|---|
| FDA SaMD / PCCP | FDA CDRH | Final guidance Dec 2024 | PCCP, TPLC, premarket authorization |
| FDA CDS Four-Criteria Test | FDA CDRH | Final guidance Jan 2026 | Clinician reviewability (Criterion 4) |
| FDA AI/ML Action Plan | FDA CDRH | Active | GMLP, real-world performance monitoring |
| ONC HTI-1 / Predictive DSI | ONC/ASTP | Effective Jan 2025 | FAVES, source attributes |
| 21st Century Cures Act | ONC/OIG | Active enforcement | FHIR APIs, information blocking prohibition |
| EU AI Act | EU AI Office | High-risk: Aug 2026 | Conformity assessment, human oversight |
| CMS MA AI Guidance | CMS | Active | No AI-sole coverage denials; bias audits |
| Colorado AI Act | Colorado AG | Effective June 30, 2026 | Impact assessments, bias risk disclosure |
| NYC Local Law 144 | NYC DCWP | Active since July 2023 | Annual bias audits for hiring AEDTs |
| HIPAA De-identification | HHS OCR | Active | Expert Determination or Safe Harbor |
| NIST AI RMF 1.0 | NIST | Active (voluntary) | GOVERN/MAP/MEASURE/MANAGE |

## Key Details

### FDA SaMD and PCCP
- ~950 FDA-cleared AI/ML devices as of 2025, growing ~100/year
- Predetermined Change Control Plans (Dec 2024): Pre-specify model modifications and validation protocols
- Total Product Lifecycle (TPLC): Continuous postmarket monitoring, drift detection
- Classification via 510(k), De Novo, or PMA based on risk

### FDA CDS Four-Criteria Test (Jan 2026 Final Guidance)
CDS is NOT a device if it:
1. Does not acquire/process medical images, signals, or patterns
2. Displays, analyzes, or prints medical information
3. Provides recommendations to an HCP
4. Enables HCP to independently review the basis (Criterion 4)

If software fails ANY criterion, it becomes a regulated device.

2026 update: Opens pathways for LLM-based tools meeting all four criteria.

### LLM/Foundation Model Status (2025-2026)
- No LLM has received FDA authorization as a medical device
- Jan 2026 CDS guidance opens non-device pathways for LLMs meeting Criterion 4
- Black-box LLMs with time-critical/directive outputs at risk of device classification
- Dedicated LLM/foundation model guidance is pending
- Hallucination, prompt sensitivity, and lack of explainability = automation bias risk

### ONC HTI-1 — Predictive DSI
- New certification criterion §170.315(b)(11): covers AI/ML from LLMs to risk calculators
- Source attributes: training data, validation, performance metrics, bias mitigation
- FAVES framework: Fairness, Appropriateness, Validity, Effectiveness, Safety
- USCDI v3 adoption: January 1, 2026
- **HTI-5 (Dec 2025)**: Proposes removing model card requirements; FHIR-first pivot

### EU AI Act
- Healthcare AI classified as high-risk under Annex III
- Staggered enforcement: Feb 2025 (prohibited practices) → Aug 2026 (high-risk) → Aug 2027 (medical device AI)
- Requirements: Conformity assessment, CE marking, technical documentation, human oversight, logging
- Fines: Up to €35M or 7% global turnover

### Algorithmic Bias
- **CMS**: AI cannot be sole basis for Medicare Advantage coverage denials
- **Colorado AI Act** (June 30, 2026): Impact assessments, bias risk disclosure to AG
- **NYC LL144**: Annual bias audits for hiring AEDTs; $500-$1,500/day penalties
- Notable exemption in Colorado: HIPAA-covered entities providing recommendations requiring HCP action

### HIPAA and AI Training Data
- **Safe Harbor**: Remove all 18 identifiers (deterministic but reduces data utility)
- **Expert Determination**: Statistical analysis by qualified expert (preferred for ML training)
- BAAs required for all AI vendors processing PHI
- Re-identification risk assessment when combining de-identified datasets

### 21st Century Cures Act
- Information blocking prohibition with eight defined exceptions
- Penalties: Up to $1M/violation for tech developers; 75% Medicare market basket reduction for hospitals
- HTI-5 proposes FHIR-first, API-only certification

### NIST AI RMF 1.0
- Four functions: Govern, Map, Measure, Manage
- AI 600-1 (2024): Generative AI-specific risks (confabulation, data privacy, bias)
- De facto governance prerequisite for healthcare AI at scale
- Referenced by FDA, CMS, and federal contracts

## Critical Dynamics (2025-2026)

**Policy tension**: Biden-era transparency requirements partially rolled back (HTI-5). FDA loosening CDS classification. Creates compliance ambiguity window.

**Enforcement is real**: Information blocking penalties, CMS MA audit programs, OIG Enforcement Alert (Sep 2025).

**EU harmonization**: EU standards tend to become global de facto standards over time.

## Sources
- FDA.gov (CDRH, SaMD, CDS guidance)
- ONC/ASTP (HTI-1, HTI-4, HTI-5)
- EU Digital Strategy (AI Act)
- CMS.gov (MA AI guidance)
- Colorado SB 24-205, NYC Local Law 144
- NIST AI RMF publications
