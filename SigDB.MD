# SignaturAnnoncePortal Database Schema Documentation

## Overview

**Database**: SignaturAnnoncePortal
**Total Tables**: 357 (with data) + 98 (empty) = 455
**Total Stored Procedures**: 60
**Schema**: dbo (all tables)

---

## Functional Domains

### 1. E-Recruitment (ER)
Core recruitment/applicant tracking system.

| Table Group | Description |
|-------------|-------------|
| `ERActivity*` | Job postings/recruitment activities |
| `ERCandidate*` | Job applicants and their data |
| `ERApplicationTemplate*` | Application form templates and fields |
| `ERJobBank*` | Talent pool/job bank management |
| `ERJobProfile*` | Job profiles and matching |
| `ERLetterTemplate*` | Email/letter templates |
| `ERInterviewAppointment` | Interview scheduling |
| `ERSmsTemplate*` | SMS templates |

### 2. Onboarding (OB)
Employee onboarding workflow system.

| Table | Description |
|-------|-------------|
| `OBProcess*` | Onboarding process instances |
| `OBTemplate*` | Onboarding templates and tasks |
| `OBLetterTemplate*` | Onboarding communication templates |

### 3. Web Advertisements (WebAd)
Job posting and web ad management.

| Table | Description |
|-------|-------------|
| `WebAd` | Web job advertisements |
| `WebAdTemplate*` | Ad templates |
| `WebAdCategory*` | Ad categories |
| `WebAdLog` | Ad activity logging |
| `WebAdJobAgt*` | Job agent/alerts |

### 4. Media & Insertions
Print media and ad placement management.

| Table | Description |
|-------|-------------|
| `Media*` | Media outlets/publications |
| `Insertion*` | Print ad insertions/bookings |
| `Placement*` | Ad placements |

### 5. External Job Boards Integration

| Integration | Tables |
|-------------|--------|
| **Jobnet** (Danish job portal) | `JobnetWebAd*`, `JobnetOccupation*`, `JobnetMunicipality*` |
| **LinkedIn** | `LinkedInWebAd*`, `LinkedInCategory`, `LinkedInSkill*` |
| **Naturejobs** | `NaturejobsWebAd*`, `NaturejobsCategory*` |

### 6. User Management & Security

| Table | Description |
|-------|-------------|
| `User` | Main user table |
| `aspnet_*` | ASP.NET membership/roles |
| `Permission*` | Permission system |
| `MitId*` | Danish MitID authentication |
| `Kombit*` | Danish Kombit roles |

### 7. Activity/Case Management

| Table | Description |
|-------|-------------|
| `Activity*` | Activities/cases |
| `Sig_Activity*` | Signatur-specific activity data |
| `Client*` | Client/organization management |

### 8. Assets & Files

| Table | Description |
|-------|-------------|
| `AssetFolder*` | Digital asset folders |
| `AssetFile*` | Digital asset files |
| `BinaryFile` | Binary file storage references |
| `CorFile*` | Correction/proofing files |

### 9. External Exports

| Table | Description |
|-------|-------------|
| `KMDOpusExport*` | KMD Opus HR system export |
| `SilkeborgData*` | Silkeborg Data integration |

---

## Top 10 Largest Tables

| Rank | Table | Rows | Size (MB) | Purpose |
|------|-------|------|-----------|---------|
| 1 | `KMDOpusExportDocument` | 52,203 | **44,377** | KMD Opus export documents (BLOB storage) |
| 2 | `ERApplicationTemplateFieldData` | 5,872,425 | **3,320** | Candidate application form field values |
| 3 | `User` | 163,945 | **1,692** | User accounts |
| 4 | `WebAdLog` | 105,368 | **1,286** | Web ad activity logs |
| 5 | `WebAd` | 74,244 | **1,247** | Job advertisements |
| 6 | `ERCandidate` | 1,315,754 | **1,156** | Job candidates/applicants |
| 7 | `UserActivityLog` | 455,087 | **1,012** | User activity audit trail |
| 8 | `ERCandidateLog` | 257,682 | **716** | Candidate activity logs |
| 9 | `AssetFolder` | 368,260 | **727** | Digital asset folder structure |
| 10 | `MitIdTransactionLog` | 86,132 | **389** | MitID authentication logs |

---

## Stored Procedures

60 procedures total, primarily:
- **ASP.NET Membership** (`aspnet_*`): User/role management (~55 procedures)
- **Custom**: `WriteLog`, `AddLogCategory`, `sp_generate_inserts_xml`

---

## Key Entity Relationships

```
Client (1534)
  └── ClientSection (30,405)
        └── ERActivity (60,722) ─── ERCandidate (1,315,754)
              │                          └── ERCandidateFile (600,880)
              │                          └── ERApplicationTemplateFieldData (5.8M)
              └── WebAd (74,244)
                    └── Insertion (124,489)
                    └── JobnetWebAd (62,177)

User (163,945)
  └── aspnet_UsersInRoles (195,055)
  └── UserActivityLog (455,087)
  └── ErCandidateUser (3,930,090) ─── links to ERCandidate
```

---

## Notes for Developers

1. **High-volume tables**: `ERApplicationTemplateFieldData`, `ERCandidate`, `ErCandidateUser` require indexed queries
2. **BLOB storage**: `KMDOpusExportDocument` stores large documents (~44GB)
3. **Audit trails**: Multiple `*Log` tables track activity history
4. **Lookup/Type tables**: Many small tables ending in `*Type`, `*Status` are reference data
5. **ASP.NET Identity**: Legacy membership system in `aspnet_*` tables
