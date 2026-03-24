# Healthcare Data Standards & Regulatory Landscape (2026)

Research for ACME Health Platform — RQ4 (Data Standards) and RQ5 (State & Industry Requirements)

**Researched**: 2026-03-21
**Scope**: What a FHIR-based health platform startup needs to know about standards, terminology licensing, and regulatory obligations

---

## Contents

- [RQ4: Data Standards & Interoperability](#rq4-data-standards--interoperability)
  - [FHIR Versions](#fhir-versions)
  - [HL7 v2](#hl7-v2--legacy-but-unavoidable)
  - [USCDI](#uscdi-us-core-data-for-interoperability)
  - [C-CDA](#c-cda-consolidated-clinical-document-architecture)
  - [SMART on FHIR](#smart-on-fhir)
  - [Bulk FHIR](#bulk-fhir--smart-backend-services)
  - [Terminology Standards](#terminology-standards-icd-snomed-loinc-rxnorm-cpt-ndc)
- [RQ5: State & Industry Requirements](#rq5-state--industry-requirements)
  - [TEFCA](#tefca)
  - [CMS Interoperability Rules](#cms-interoperability-rules)
  - [CARIN Alliance](#carin-alliance)
  - [State Privacy Laws](#state-privacy-laws)
  - [HIEs & Payer-to-Payer Exchange](#hies--payer-to-payer-exchange)
- [Portfolio Relevance Summary](#portfolio-relevance-summary)

---

## RQ4: Data Standards & Interoperability

### FHIR Versions

| Version | Status | ONC/CMS Mandated | Recommendation |
|---------|--------|-------------------|----------------|
| R4 (4.0.1) | Dominant | Yes — 21st Century Cures Act, HTI-1 | **Build on R4** |
| R4B | Niche | No | Skip unless medication regulation or EBM use case |
| R5 | Trial-use, limited adoption | No | Skip for now |
| R6 | Pre-release, normative ballot Jan 2026 | No | Plan migration ~2027+ |

**What it covers**: FHIR (Fast Healthcare Interoperability Resources) is the current API standard for health data exchange. It uses RESTful APIs, JSON/XML, and modular "resources" (Patient, Observation, MedicationRequest, etc.).

**Applicability**: Mandatory for ONC-certified health IT. Required by CMS for payer APIs (Patient Access, Provider Access, Prior Authorization). The 21st Century Cures Act specifies FHIR R4 by version number.

**Key requirements**:
- All certified EHRs must expose FHIR R4 APIs (§170.315(g)(10))
- US Core Implementation Guide profiles constrain FHIR R4 resources to USCDI data elements
- USCDI v3 compliance via FHIR R4 required January 1, 2026

**Portfolio relevance**: Foundation. Build the ACME Health FHIR server on R4. Use US Core IG profiles as the data model baseline. Do not build on R5 — regulatory compliance requires R4.

---

### HL7 v2 — Legacy but Unavoidable

**Governing body**: HL7 International
**Current version**: v2.9 (but v2.3–v2.8 dominate in practice)

**What it covers**: Pipe-delimited message format for real-time operational data — ADT (admit/discharge/transfer), lab orders/results (ORM/ORU), scheduling (SIU), billing (DFT). The internal nervous system of most hospitals.

**Applicability**: Not mandated by modern rules, but de facto required for EHR integration. Used in ~95% of US healthcare organizations. You will encounter it when integrating with labs, RIS/LIS systems, and legacy EHRs.

**Key requirements**: No federal mandate to produce HL7 v2. However, any platform ingesting data from hospital systems must be able to consume v2 messages. Integration engines (Mirth Connect, Rhapsody, Azure Health Data Services) handle v2-to-FHIR translation.

**Portfolio relevance**: Differentiator. ACME Health does not need to originate HL7 v2 but should support inbound v2 parsing for EHR data ingestion. Use an integration engine layer; do not build a v2 parser from scratch.

---

### USCDI (US Core Data for Interoperability)

**Governing body**: ONC (Office of the National Coordinator for Health IT) / ASTP
**Current mandated version**: v3.1 (effective January 1, 2026 per HTI-1 Final Rule)
**Latest published**: v6 (July 2025, voluntary via SVAP); Draft v7 (January 2026, public comment)

**What it covers**: The minimum set of health data elements that certified health IT must be able to exchange. Organized into Data Classes (groups) and Data Elements.

**v3.1 Data Classes** (required by Jan 1, 2026):
- Clinical Notes, Diagnostic Imaging, Encounter Information, Goals, Health Concerns, Immunizations, Laboratory, Medications, Patient Demographics, Problems, Procedures, Provenance, Referral Notes, Smoking Status, Unique Device Identifiers, Vital Signs

Note: v3.1 removed sex/gender identity data elements (pronouns, sexual orientation, gender identity) per EO 14168 enforcement discretion guidance issued March 2025.

**USCDI v5 additions** (voluntary, approved for SVAP Aug 2025): Orders data class (addresses medication reconciliation at care transitions), expanded laboratory observations.

**USCDI v6 additions** (voluntary, July 2025): Care Plan, Portable Medical Order (DNR/POLST), Unique Device Identifier, Facility Address, Date of Onset, Family Health History.

**Applicability**: Mandatory for ONC-certified health IT. TEFCA requires USCDI v3 for all data exchanged via QHIN networks as of January 1, 2026. Information blocking violations can reach $1M per patient.

**Portfolio relevance**: Mandatory. Map all ACME Health data models to USCDI v3.1 data elements. Implement v3.1 as the compliance floor; track v5/v6 for differentiation (Care Plan, UDI support signals enterprise readiness).

---

### C-CDA (Consolidated Clinical Document Architecture)

**Governing body**: HL7 International
**Current version**: C-CDA R2.1 (still in active use); C-CDA on FHIR is the bridge standard

**What it covers**: XML-based document format for complete patient summaries — Continuity of Care Documents (CCD), Discharge Summaries, Progress Notes, Referral Notes. Over 500 million C-CDA documents exchanged annually in the US.

**Applicability**: Required. Certified EHRs must generate C-CDA documents for care transitions (Meaningful Use/Promoting Interoperability Stage 2+). Remains the primary format for Direct messaging and HIE document exchange. Not going away — ONC-certified EHRs have been required to support it since 2014.

**Key requirements**: Platforms receiving data from EHRs must be able to parse C-CDA. C-CDA on FHIR (a US Realm specification) allows C-CDA documents to be represented as FHIR Document Bundles, enabling gradual migration.

**Portfolio relevance**: Recommended. ACME Health should be able to ingest C-CDA documents from EHRs (parse XML, map to FHIR resources). Build a C-CDA ingest pipeline. Do not originate new C-CDA; FHIR is the forward-looking output format.

---

### SMART on FHIR

**Governing body**: HL7 International / SMART Health IT (Boston Children's Hospital)
**Current version**: SMART App Launch v2.0 (ONC SVAP 2022 approved)

**What it covers**: Authorization framework for health apps. Combines OAuth 2.0 + OpenID Connect + PKCE to allow third-party apps to securely access EHR data. Defines two launch models: EHR Launch (within EHR context) and Standalone Launch. Defines granular FHIR scopes (e.g., `patient/Observation.read`).

**Applicability**: Mandatory. ONC's 21st Century Cures Act Final Rule requires certified EHRs to support SMART App Launch for the standardized API criterion (§170.315(g)(10)). ONC G10 certification deadline: December 31, 2025. CMS-0057-F requires SMART Backend Services for the Provider Access and Payer-to-Payer APIs.

**Key requirements**:
- Apps accessing EHR data must implement SMART App Launch v2.0
- Scoped access model: request minimum needed scopes
- PKCE required for all app types
- Backend Services (for server-to-server) uses JWKS/asymmetric keys instead of client secrets

**Portfolio relevance**: Mandatory. If ACME Health exposes a FHIR API or connects to EHRs, implement SMART App Launch v2.0 for patient-facing apps and SMART Backend Services for payer/provider integrations.

---

### Bulk FHIR / SMART Backend Services

**Governing body**: HL7 International / ONC
**Standard**: FHIR Bulk Data Access (Flat FHIR) v1.0.0 STU 1 / v2.0.0 STU 2
**Authorization**: SMART Backend Services (OAuth 2.0 with JWKS)

**What it covers**: Asynchronous, population-scale export of FHIR resources. Instead of individual API calls, a single kick-off request triggers an async export job that produces NDJSON files. Used for: population health analytics, quality measure reporting, research datasets, claims + clinical data combination, payer-to-payer bulk transfer.

**Applicability**: Required for ONC §170.315(g)(10) certification (multiple patient services). Required for CMS-0057-F Provider Access API (bulk access to member rosters). Compliance deadline: December 31, 2025 (USCDI v3 / US Core 6.1.0).

**Key requirements**:
- Supports `$export` operation at system, group, or patient level
- Output format: NDJSON (one FHIR resource per line)
- Authorization via SMART Backend Services (no user login; machine-to-machine)
- Must support `_since` parameter for incremental exports

**Portfolio relevance**: Differentiator for enterprise. Population health and analytics features require Bulk FHIR. Implement for the Provider Access API compliance path. Demonstrates enterprise readiness to payer clients.

---

### Terminology Standards (ICD, SNOMED, LOINC, RxNorm, CPT, NDC)

#### Quick Reference

| Code System | Authority | License | Cost | Primary Use | USCDI Required |
|-------------|-----------|---------|------|-------------|----------------|
| ICD-10-CM | CDC / CMS | No | Free | Diagnosis/billing | Yes (problems, encounter dx) |
| ICD-11 | WHO | No | Free | Global diagnosis | Not yet mandated in US |
| SNOMED CT | SNOMED Intl / NLM | UMLS (free in US) | Free (US) | Clinical documentation | Yes (problems, procedures) |
| LOINC | Regenstrief Institute | Yes | Free (commercial ok) | Lab tests, observations, vitals | Yes (lab, vitals) |
| RxNorm | NLM (NIH) | UMLS (free) | Free | Drug naming, prescriptions | Yes (medications) |
| CPT | AMA | Mandatory paid | ~$19.50/provider/yr + royalties | Procedures/billing | No (billing only) |
| NDC | FDA | No | Free | Drug product identification | No |

#### Detail Notes

**ICD-10-CM**: Maintained by CDC and CMS. 70,000+ diagnosis codes. Required for USCDI (problems list, encounter diagnoses). US implementation of ICD-10 with clinical modifications. Annual updates (October 1). ICD-11 transition: WHO adopted in 2022; US has no firm migration deadline as of 2026 — continue building on ICD-10-CM.

**SNOMED CT**: 350,000+ concepts covering all clinical findings, symptoms, procedures, body structures. The richest clinical terminology. Free in the US via NLM UMLS license. Required for USCDI problem list and procedure coding. FHIR ValueSets reference SNOMED extensively. License: register at NLM UTS (free).

**LOINC**: 100,000+ codes for lab tests, clinical measurements, vital signs, imaging, and survey instruments. Free for commercial use — most startup-friendly terminology. Required for USCDI lab results and vital signs. Updated twice yearly (February, August).

**RxNorm**: Normalized drug names with unique RXCUIs. Links brand and generic names, dose forms, strengths. Free via UMLS license. Updated weekly (new FDA approvals). Required for USCDI medications. Integrates with NDC for product-level identification.

**CPT**: The only major code set requiring a paid commercial license (AMA copyright). Required if your platform displays procedure codes, generates claims, or integrates with billing workflows. 2025 pricing: ~$19.50/provider/year; enterprise royalty fees apply. Budget for this early — it affects cost models significantly.

**NDC**: 11-digit codes identifying specific drug products (manufacturer + product + package). Free from FDA NDC Directory. Used in pharmacy claims, medication dispense records. RxNorm maps to NDC for product-level lookups.

**ICD-11 transition**: Not yet mandated in the US. WHO released ICD-11 in 2022; US adoption planning is underway but no compliance deadline announced. Build on ICD-10-CM; architect to support ICD-11 later.

---

## RQ5: State & Industry Requirements

### TEFCA

**Full name**: Trusted Exchange Framework and Common Agreement
**Governing body**: ONC / ASTP; Recognized Coordinating Entity (RCE): The Sequoia Project
**Status**: Live. 11 designated QHINs as of late 2025; 10,600+ participating organizations.

**What it covers**: Nationwide health information exchange network-of-networks. Defines common rules for how Qualified Health Information Networks (QHINs) connect and exchange data. Enables query-based exchange (finding records), alert-based exchange (ADT notifications), and document exchange across any participating organization — regardless of which QHIN they belong to.

**Current QHINs** (as of November 2025): eHealth Exchange, Epic Nexus, Health Gorilla, KONZA National Network, MedAllies, Oracle Health Information Network, and others (11 total).

**Applicability**: Voluntary for most participants. Mandatory for QHINs to sign the Common Agreement. USCDI v3 required for all TEFCA data exchange as of January 1, 2026. A final rule on QHIN qualification became effective January 15, 2025.

**Key requirements**:
- Exchange Purposes: Treatment, Payment, Healthcare Operations, Individual Access, Public Health, Government Benefits Determination (added December 2025)
- QHIN Technical Framework v2.1 (effective December 4, 2025): adds directed query to specific nodes
- Applications to become a QHIN: rolling basis, ~12-month process
- Common Agreement Version 2 published May 1, 2024

**Portfolio relevance**: High. Connecting ACME Health to a QHIN (likely as a Subparticipant under an existing QHIN, not a QHIN itself) enables nationwide patient record retrieval. Target becoming a TEFCA Subparticipant via Health Gorilla or eHealth Exchange. This unlocks real clinical data for demo and production use.

---

### CMS Interoperability Rules

#### CMS-9115-F (2020) — Patient Access & Provider Directory

**What it requires**: Impacted payers (Medicare Advantage, Medicaid managed care, CHIP, FFE QHPs) must implement:
- Patient Access API: member claims + clinical data via FHIR R4 + SMART on FHIR
- Provider Directory API: public FHIR-based directory of contracted providers

**Status**: In effect since 2021. This rule established the foundation that CMS-0057-F builds on.

#### CMS-0057-F (2024) — Interoperability and Prior Authorization

**Governing body**: CMS
**Effective**: January 1, 2024 (final rule published); compliance timelines phased

**What it requires**: Four FHIR R4 APIs for impacted payers:

| API | Purpose | Compliance Deadline |
|-----|---------|---------------------|
| Patient Access API (enhanced) | Claims, clinical data, prior auth status to members | January 1, 2027 |
| Provider Access API | Member data to in-network providers (individual + bulk) | January 1, 2027 |
| Provider Directory API | Public directory, updated within 30 days | January 1, 2027 |
| Prior Authorization API | Electronic PA requests/responses; requirement checks | January 1, 2027 |

**Operational requirements** (in effect January 1, 2026):
- PA decisions: 72 hours for urgent, 7 calendar days for standard
- First public PA metrics report due March 31, 2026 (covering CY2025)

**Who is affected**: Medicare Advantage organizations, Medicaid/CHIP FFS and managed care, QHP issuers on FFEs. Commercial-only payers not directly covered but will face market pressure.

**Technical standards**: All APIs must use FHIR R4. HIPAA flexibility: payers implementing all-FHIR PA API do not need to use X12 278 (HIPAA Administrative Simplification enforcement discretion applies).

**Implementation guides referenced**:
- US Core IG (clinical data)
- CARIN IG for Blue Button (claims data)
- Da Vinci Prior Authorization Support (PAS) IG
- Da Vinci Coverage Requirements Discovery (CRD) IG
- Da Vinci Documentation Templates and Rules (DTR) IG

**Estimated savings**: $15 billion over 10 years (CMS projection).

**Portfolio relevance**: Direct. If ACME Health builds payer-facing tools or connects to payer APIs, all four endpoints are relevant. The PA API is the most technically complex and highest-value integration opportunity. Prior authorization automation is the biggest pain point in US healthcare administration.

---

### CARIN Alliance

**Full name**: Creating Access to Real-time Information Now through Consumer-Directed Exchange
**Type**: HL7 FHIR Accelerator program (industry coalition)
**Key output**: CARIN IG for Blue Button (CARIN BB IG) — the implementation guide for payer Patient Access APIs

**What it covers**: Defines how payers expose adjudicated claims and encounter data (Explanation of Benefit / EOB resources) to consumers via FHIR APIs. The Common Payer Consumer Data Set (CPCDS) maps claims data to FHIR ExplanationOfBenefit profiles.

**Current version**: CARIN BB IG STU 2.1.0 (published February 2025) — adds US Core 6.1.0 support, oral/vision claims profiles.

**Applicability**: Recommended / de facto required. CMS-9115-F and CMS-0057-F direct payers to implement Patient Access APIs using CARIN IG for Blue Button as the reference implementation guide. CMS Blue Button 2.0 (the Medicare program) covers 53M+ FFS beneficiaries.

**Key requirements**:
- ExplanationOfBenefit (EOB) FHIR resource is the core data structure
- Must include dental and vision claims (added in STU 2.0+)
- Consumer-directed exchange invokes HIPAA Individual Right of Access (45 CFR 164.524)
- SMART on FHIR authorization required for patient-facing apps accessing EOBs

**Portfolio relevance**: Recommended. If ACME Health aggregates payer data (claims history, prior auth status, coverage), implement CARIN BB IG as the consumption standard. Enables building patient-facing financial health views and care gap analysis from claims data.

---

### State Privacy Laws

#### California CCPA / CPRA

**Governing body**: California Privacy Protection Agency (CPPA) + AG enforcement
**Effective**: January 1, 2023 (CPRA amendments to CCPA)
**Applicability threshold**: Annual revenue >$25M, OR data of 100,000+ California residents, OR 50%+ revenue from selling/sharing personal data

**HIPAA exemption — critical nuances**:
- HIPAA-covered entities: PHI is exempt from CCPA/CPRA
- Exemption is NOT blanket: employee data, web analytics, non-PHI health data all fall under CCPA/CPRA
- HIPAA Business Associates: not exempt (only covered entities are)
- Non-HIPAA health apps (fitness, wellness, consumer health): fully subject to CCPA/CPRA
- Once PHI leaves a covered entity to a non-CE/BA third party, California law may apply

**Key compliance requirements**:
- Separate privacy notice for non-PHI data (can't rely on HIPAA NPP alone)
- Specific consent for collection, sharing, and sale of health data
- Consumer rights: access, deletion, opt-out of sale/sharing
- Private right of action for data breaches (significant litigation risk)

**Portfolio relevance**: High. ACME Health likely falls under CCPA/CPRA for any California users, especially if collecting wellness or behavioral data outside the strict PHI definition. Assume CCPA applies unless legal confirms the exemption applies to your specific data flows.

#### Washington My Health My Data Act (MHMDA)

**Governing body**: Washington AG + private right of action
**Effective**: Large businesses March 31, 2024; small businesses June 30, 2024; geofencing July 2023
**Applicability**: Any entity doing business in or serving Washington consumers — NO minimum revenue or data volume threshold for large businesses

**What it covers**: Personal health data outside HIPAA's scope — fitness data, location data that could infer health-seeking behavior, reproductive health, mental health, derived health inferences from non-health data.

**Key requirements**:
- Separate consumer health data privacy policy (must be linked from homepage)
- Specific, separate consent for each collection purpose
- 6-year retention of data sale authorizations
- Geofencing ban around healthcare facilities
- Consumer rights: access, deletion, withdrawal of consent
- Per se violation of Washington Consumer Protection Act (private right of action)

**Portfolio relevance**: Critical risk. MHMDA has the broadest scope of any US state health privacy law. No size threshold. Private right of action. If ACME Health is available in Washington state, compliance is mandatory. Especially relevant for any non-HIPAA health data (behavioral signals, engagement data, wellness features).

#### Nevada Consumer Health Data Privacy Law

Similar to MHMDA but narrower definition of consumer health data; no private right of action. Effective March 31, 2024.

#### Other States to Watch

Multiple states have proposed or enacted similar laws. The trend is toward state-level health data privacy laws filling gaps left by HIPAA's limited scope. Compliance frameworks built for Washington MHMDA will generally cover most similar state laws.

---

### HIEs & Payer-to-Payer Exchange

#### Health Information Exchanges (HIEs)

**What they are**: State or regional organizations that aggregate and share clinical data among participating providers. Most states have at least one HIE; some are state-mandated (e.g., New York's Statewide Health Information Network, California's DxF).

**State mandates**: Variable. Some states mandate provider participation (e.g., New York). Others are voluntary but incentivized. California's Data Exchange Framework (DxF) requires health facilities to share data via TEFCA-aligned standards.

**Portfolio relevance**: Differentiator. Connecting to local HIEs provides real patient data for care coordination features. Many HIEs now expose FHIR APIs alongside legacy interfaces. TEFCA Subparticipant status often provides access to HIE networks nationally.

#### Payer-to-Payer Data Exchange

**Requirement**: CMS-0057-F requires impacted payers to implement a Payer-to-Payer API (P2P) by January 1, 2027. When a member switches health plans, the new payer can request clinical and claims data from the prior payer (with member consent, covering up to 5 years of history).

**Technical standard**: FHIR R4 + US Core IG + Da Vinci Member Attribution List (ATRList) IG.

**Portfolio relevance**: High for payer-facing products. The P2P API creates a new data source for longitudinal patient records. Health tech platforms that help payers implement P2P exchange have a clear market opportunity before the January 2027 deadline.

---

## Portfolio Relevance Summary

### Mandatory (must implement for any FHIR-based health platform)

| Standard | Why |
|----------|-----|
| FHIR R4 | Regulatory baseline; all CMS/ONC mandates reference R4 |
| USCDI v3.1 | Required for ONC certification and TEFCA exchange by Jan 1, 2026 |
| US Core IG | Profiles FHIR R4 resources to USCDI elements |
| SMART on FHIR v2.0 | Required for EHR API access and ONC G10 certification |
| ICD-10-CM, LOINC, SNOMED CT, RxNorm | USCDI-required terminologies; all free in US |

### Recommended (differentiators or compliance for payer/provider integrations)

| Standard | Why |
|----------|-----|
| Bulk FHIR / SMART Backend Services | Required for population health, provider access API |
| CARIN IG for Blue Button | Standard for consuming payer claims data |
| C-CDA ingest | Required to receive data from legacy EHR systems |
| TEFCA Subparticipant | Nationwide patient record access |
| CMS-0057-F PA API | Highest-value payer integration opportunity |

### Compliance (legal obligations)

| Standard | Why |
|----------|-----|
| California CCPA/CPRA | Likely applies to non-PHI data collected from CA users |
| Washington MHMDA | Applies if serving WA consumers; no size threshold; private right of action |
| CPT licensing | Required if displaying procedure codes or integrating with billing |

### Watch / Future Planning

| Standard | Why |
|----------|-----|
| USCDI v5/v6 | Voluntary now; track Care Plan and Orders classes |
| FHIR R6 | Normative ballot January 2026; plan migration ~2027 |
| ICD-11 | No US deadline set; architect for eventual migration |
| TEFCA QHIN designation | If ACME Health reaches scale requiring direct QHIN status |

---

## Sources

- [USCDI - ONC ISP](https://isp.healthit.gov/united-states-core-data-interoperability-uscdi)
- [USCDI V3 Compliance Is Mandatory by 2026](https://www.imohealth.com/resources/uscdi-v3-compliance-is-mandatory-by-2026-heres-how-to-get-ready/)
- [FHIR R4 vs R5 Choosing the Right Version](https://www.health-samurai.io/articles/fhir-r4-vs-fhir-r5-choosing-the-right-version-for-your-implementation)
- [The State of FHIR in 2025](https://fire.ly/blog/the-state-of-fhir-in-2025/)
- [CMS-0057-F Fact Sheet](https://www.cms.gov/newsroom/fact-sheets/cms-interoperability-prior-authorization-final-rule-cms-0057-f)
- [CMS-0057-F Decoded: Must-Have APIs vs Nice-to-Have IGs for 2026-2027](https://fire.ly/blog/cms-0057-f-decoded-must-have-apis-vs-nice-to-have-igs-for-2026-2027/)
- [TEFCA - ASTP/ONC](https://www.healthit.gov/topic/interoperability/policy/trusted-exchange-framework-and-common-agreement-tefca)
- [TEFCA RCE - Sequoia Project](https://rce.sequoiaproject.org/tefca/)
- [TEFCA Updates Presentation Feb 2026](https://healthit.gov/wp-content/uploads/2026/02/2026-02-19_TEFCA_Updates_Presentation_508.pdf)
- [SMART on FHIR App Launch v2.0](https://docs.smarthealthit.org/)
- [ONC Standardized API Certification Criterion](https://www.healthit.gov/test-method/standardized-api-patient-and-population-services)
- [Bulk Data Access IG - HL7](https://hl7.org/fhir/uv/bulkdata/)
- [CARIN IG for Blue Button STU 2.1.0](https://build.fhir.org/ig/HL7/carin-bb/)
- [Washington My Health My Data Act - RCW 19.373](https://app.leg.wa.gov/RCW/default.aspx?cite=19.373&full=true)
- [Washington MHMDA - Goodwin Law](https://www.goodwinlaw.com/en/insights/publications/2024/03/alerts-technology-hltc-my-health-my-data-act-mhmda)
- [CCPA HIPAA Exemption - HIPAA Journal](https://www.hipaajournal.com/ccpa-hipaa-exemption/)
- [CPRA Healthcare Entities - Quarles Law](https://www.quarles.com/newsroom/publications/cpra-is-in-effect-what-health-and-life-sciences-entities-need-to-know)
- [CPT Licensing FAQs - AMA](https://www.ama-assn.org/practice-management/cpt/cpt-licensing-frequently-asked-questions-faqs)
- [SNOMED CT Licensing - NLM](https://www.nlm.nih.gov/healthit/snomedct/snomed_licensing.html)
- [Medical Coding Systems Explained - IMO Health](https://www.imohealth.com/resources/medical-coding-systems-explained-icd-10-cm-cpt-snomed-and-others/)
- [HL7 v2 vs FHIR 2025 - Healthcare Integrations](https://healthcareintegrations.com/hl7-vs-fhir-which-standard-should-you-prioritize-in-2025/)
- [C-CDA vs FHIR - Metriport](https://www.metriport.com/blog/c-cda-and-fhir-key-differences)
