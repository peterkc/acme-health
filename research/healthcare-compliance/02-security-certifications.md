# Security Certifications & Frameworks

**Worker**: RQ2 | **Date**: 2026-03-21

## Framework Comparison

| Framework | Type | Timeline | Year 1 Cost | Healthcare Applicability |
|---|---|---|---|---|
| SOC 2 Type II | Certification | 6-12 months | $30K-$150K | De facto required for enterprise |
| HITRUST r2 | Certification | 12-15 months | $100K-$200K | Gold standard; mandatory for large buyers |
| HITRUST i1 | Certification | 6-9 months | $40K-$80K | Startup entry to HITRUST ecosystem |
| ISO 27001 | Certification | 9-12 months | $35K-$80K | International markets |
| NIST CSF 2.0 | Framework | Internal only | $0-$50K | Governance overlay |
| FedRAMP Moderate | Authorization | 18-24 months | $500K-$1.5M | Federal agency sales |
| FedRAMP High | Authorization | 2-3 years | Up to $2M | Federal healthcare (VA, CMS) |
| FedRAMP 20x | Authorization | ~12 months | TBD (lower) | Startup path to federal |
| NIST 800-53 | Control catalog | N/A | Embedded | FedRAMP foundation |
| NIST 800-171 | Standard | 6-12 months | $50K-$200K | DoD healthcare only |
| StateRAMP/GovRAMP | Authorization | 12-18 months | $100K-$400K | State Medicaid, public health |

## Detailed Analysis

### SOC 2 Type II (AICPA)
- Five Trust Services Criteria: Security, Availability, Confidentiality, Processing Integrity, Privacy
- Type II tests over observation window (3-12 months)
- Start with Security-only scope to reduce initial cost
- 50-70% of control work reusable for HITRUST
- Reports valid 12 months; annual re-audit required

### HITRUST CSF r2
- Meta-framework: 50+ authoritative sources (HIPAA, NIST, ISO, PCI DSS, GDPR)
- Three tiers: e1 (~44 controls), i1 (~182), r2 (300-400)
- r2: 2-year validity with year-1 interim reassessment
- AI Security Certification now available (44 requirement statements)
- 99.41% of certified environments reported no breach (2024 Trust Report)
- Cloud control inheritance: up to 70-80% from AWS/Azure/GCP

### ISO 27001:2022
- 93 controls in Annex A; risk-based approach
- Extensions: ISO 27017 (cloud), 27018 (PII in cloud), 27701 (privacy/GDPR)
- 3-year certificate; annual surveillance audits
- Lower priority for US-only startups; critical for EU expansion

### NIST CSF 2.0 (February 2024)
- Six functions: Govern (new), Identify, Protect, Detect, Respond, Recover
- Not a certification — internal governance tool
- HHS publishes official HIPAA-to-CSF crosswalk
- Use as governance overlay while pursuing formal certifications

### FedRAMP
- Three levels: Low, Moderate, High
- FedRAMP 20x pilot (2024): viable startup path, ~12 months
- Built on NIST 800-53 Rev. 5
- Unlocks VA (largest US integrated health system)

### StateRAMP/GovRAMP (rebranded February 2025)
- 23 states currently participating
- Targets: State Medicaid, public health, health exchanges
- FedRAMP authorization enables fast-track (50-70% inheritance)

## Recommended Certification Order

1. **Phase 1 (0-12 mo)**: SOC 2 Type II — Security TSC only
2. **Phase 2 (12-24 mo)**: HITRUST i1 → upgrade to r2 as customer base scales
3. **Phase 3 (24-36 mo)**: ISO 27001 (international) or FedRAMP 20x (federal)
4. **Phase 4 (as needed)**: NIST 800-171/CMMC (DoD), StateRAMP (state contracts)
5. **Always**: NIST CSF 2.0 as internal governance overlay

## Cross-Framework Relationships

```
NIST CSF 2.0 (governance overlay)
     |
     +-- NIST 800-53 (control catalog)
          |
          +-- FedRAMP (federal cloud authorization)
          |    |
          |    +-- StateRAMP/GovRAMP (state cloud)
          |
          +-- HITRUST CSF (healthcare meta-framework)
               |
               +-- HIPAA (subsumes)
               +-- ISO 27001 (mapped)
               +-- SOC 2 (50-70% overlap)
```

## Sources
- HITRUST Alliance, A-LIGN, Censinet, AICPA
- FedRAMP.gov, NIST publications
- Cloudticity, Thoropass, ComplyJet (cost analysis)
