# Federal Regulations & Operational Compliance

**Worker**: RQ1 + RQ6 | **Date**: 2026-03-21

## Core Federal Regulations

### HIPAA Privacy Rule
- **Governing body**: HHS Office for Civil Rights (OCR)
- **Citation**: 45 CFR Parts 160 and 164
- **Covers**: National standards protecting individually identifiable health information (PHI)
- **Applicability**: Mandatory for covered entities and business associates
- **Key requirements**: Limits PHI uses/disclosures, grants patient access rights, Minimum Necessary standard
- **2024 update**: Reproductive Health Care Privacy final rule (April 26, 2024)

### HIPAA Security Rule
- **Governing body**: HHS OCR
- **Citation**: 45 CFR Part 164, Subpart C
- **Covers**: National standards for protecting ePHI through three safeguard categories
- **Applicability**: Mandatory
- **Safeguards**:
  - Administrative: Risk analysis, workforce training, access authorization, incident response, contingency planning
  - Physical: Facility access controls, workstation security, device/media controls
  - Technical: Access controls (unique user IDs, auto logoff, encryption), audit controls, integrity, transmission security
- **December 2024 NPRM** (proposed, not yet final):
  - Remove "required vs. addressable" distinction — all become required
  - Annual compliance audits (every 12 months)
  - Annual technology asset inventory and network mapping
  - 72-hour ePHI restoration requirement
  - Annual verification of BA compliance

### HIPAA Breach Notification Rule
- **Governing body**: HHS OCR
- **Citation**: 45 CFR §§164.400-414
- **Covers**: Notification requirements after breach of unsecured PHI
- **Four-factor risk assessment**:
  1. Nature/extent of PHI involved
  2. Who the unauthorized person was
  3. Whether PHI was actually acquired/viewed
  4. Extent to which risk was mitigated
- **Notification timelines**:
  - Individuals: ≤60 calendar days after discovery
  - HHS (500+ individuals): Within 60 days
  - HHS (<500): Annual log by March 1
  - Media (500+ in one state): Within 60 days

### HITECH Act
- **Governing body**: HHS OCR
- **Citation**: ARRA 2009, Pub.L. 111-5
- **Covers**: Strengthened HIPAA enforcement, tiered penalties, extended obligations to BAs
- **Penalty tiers** (annual caps):
  - Tier 1 (no knowledge): $100/violation, $25K cap
  - Tier 2 (reasonable cause): $1,000/violation, $100K cap
  - Tier 3 (willful neglect, corrected): $10,000/violation, $250K cap
  - Tier 4 (willful neglect, not corrected): $50,000/violation, $1.5M cap
- Criminal penalties: Up to 10 years for malicious PHI access

### 42 CFR Part 2 — Substance Use Disorder Records
- **Governing body**: SAMHSA + HHS OCR
- **Covers**: Extra-strength confidentiality for SUD treatment records
- **2024 Final Rule** (effective April 16, 2024; compliance by February 16, 2026):
  - Patient written consent required before disclosure
  - HIPAA Breach Notification now applies to Part 2 programs
  - Enforcement aligned with HIPAA penalties
  - SUD counseling notes: heightened protection tier

### FTC Health Breach Notification Rule
- **Governing body**: FTC
- **Citation**: 16 CFR Part 318 (2024 amendments effective July 29, 2024)
- **Covers**: Non-HIPAA health tech (apps, connected devices, wellness platforms)
- **Key**: "Breach of security" now includes unauthorized disclosures
- **Penalties**: Up to $51,744 per violation
- **Timelines**: Individuals ≤60 days; FTC (500+) ≤10 business days

### ACA Section 1557
- **Governing body**: HHS OCR + CMS
- **2024 Final Rule** (most provisions effective July 5, 2024)
- **AI provision**: Covered entities cannot discriminate using patient care decision support tools, including AI — compliance by May 1, 2025
- **Litigation note**: Some sex discrimination provisions enjoined; AI provisions remain in force

## Operational Compliance

### Business Associate Agreements (BAAs)
- Required any time PHI is disclosed to external parties
- 8 required clauses per 45 CFR §164.504(e)
- Absence has generated settlements $31K-$1.55M
- Must be executed BEFORE receiving any PHI

### Incident Response Plans
- Written plan with documented procedures required (§164.308(a)(6))
- Breach determination using four-factor risk assessment
- All incidents logged, retained 6 years
- Proposed NPRM: 72-hour restoration, annual testing

### Workforce Training
- ALL workforce members, no role exemptions
- Within reasonable period of hire
- Content: malware protection, login monitoring, password management, security reminders
- Retain documentation 6 years minimum

### Access Controls
- Unique user identification (no shared logins)
- Automatic logoff after inactivity
- Encryption at rest and in transit
- Role-based access control (RBAC)
- Minimum Necessary standard for all PHI access

### Audit Logging
- Log all ePHI access: who, what, when, how
- Successful and unsuccessful login attempts
- Automated alerts for suspicious activity
- 6-year minimum retention

### Data Retention
- HIPAA compliance documents: 6 years
- Medical records: State law governs (7-30 years depending on state)
- Key states: CA (7yr), TX (7-10yr), NY (6yr), MA (30yr)

### Risk Assessments
- Required administrative safeguard (§164.308(a)(1))
- Must cover all ePHI regardless of location
- Missing/inadequate risk assessments = ~68% of HIPAA penalty settlements
- Proposed NPRM: Make annual requirement explicit

## Sources
- HHS.gov HIPAA for Professionals (Privacy, Security, Breach Notification)
- FTC.gov Health Breach Notification Rule
- SAMHSA/HHS 42 CFR Part 2 Final Rule
- Federal Register: ACA Section 1557, HIPAA Security Rule NPRM
- HIPAA Journal, The HIPAA Guide (enforcement data)
