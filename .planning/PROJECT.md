# SignaturPortal Modernization

## What This Is

A modernization of AtlantaSignatur, a Danish job advertisement and recruitment portal (SaaS multi-tenant ATS), from ASP.NET WebForms (.NET Framework 4.8) to .NET 10 + Blazor. The migration is incremental and module-by-module using YARP reverse proxy to route between old and new solutions, starting with the E-recruitment portal core workflow.

## Core Value

The activity list view must work perfectly. Users must be able to see and navigate their recruitment activities with correct status. This is the main view of the E-recruitment portal and the heart of the applicant tracking system.

## Requirements

### Validated

(None yet — ship to validate)

### Active

**Foundation Infrastructure:**
- [ ] YARP reverse proxy configured to route between original and modernized solutions
- [ ] System.Web Adapters sharing session state between original and modern
- [ ] System.Web Adapters sharing authentication between original and modern
- [ ] Navigation shell with top nav matching original UI look and feel
- [ ] Clean architecture structure (Infrastructure/Domain/Application layers)
- [ ] EF Core setup with repository pattern for database access
- [ ] DTO pattern with manual mapping (no AutoMapper)
- [ ] Unit test framework setup (TUnit)
- [ ] Playwright E2E test framework setup

**E-recruitment Portal - Core Workflow:**
- [ ] Activity list view displays recruitment activities with correct status
- [ ] Activity creation (new recruitment activity)
- [ ] Activity editing (update recruitment activity details)
- [ ] Activity deletion (remove recruitment activity)
- [ ] Application viewing (view CVs and application letters for an activity)
- [ ] Application storage (ensure applications are properly persisted)
- [ ] Hiring team display (show team members for an activity)
- [ ] Multi-tenancy support (Site/Client two-level isolation)
- [ ] Role and permission enforcement (staff vs client user access)
- [ ] Localization using GetText + DB tables (matching original approach)

**Testing & Verification:**
- [ ] Playwright E2E tests pass against both original and modernized solutions
- [ ] Performance matches or improves on original solution
- [ ] UI matches original look and feel (Blazor + MudBlazor components)

### Out of Scope

- Advanced screening workflow features — defer to v2
- Interview management features — defer to v2
- Job advertisement portal — defer to future milestone
- Onboarding/Offboarding portal — defer to future milestone
- Job bank portal — defer to future milestone
- Modern authentication migration — stays with System.Web Adapters until all modules migrated
- Database schema alterations — preserve existing schema, only add new tables if absolutely necessary
- LLBLGen ORM migration — use EF Core instead, don't port ORM code
- Dead code from original — selective migration, don't port unnecessary legacy code

## Context

**Original System:**
- Codebase location: `C:\Users\it\Documents\Dev\CustomersAndProjects\Signatur\Dev3Org\AtlantaSignatur`
- Codebase mapping: `C:\Users\it\Documents\Dev\CustomersAndProjects\Signatur\Dev3Org\.planning\codebase`
- Database: SignaturAnnoncePortal (SQL Server, existing production data)
- Primary customers: Large Danish organizations with multiple departments and many users

**Portal Modules (in original system):**
1. **Job advertisement portal** — Consultancy workflow for managing job ads across media
2. **E-recruitment portal (ATS)** — Applicant tracking with hiring teams, screening, interviews
3. **Onboarding/Offboarding portal** — Employee task management and HR processes
4. **Job bank portal** — Job listings with custom application forms

**Multi-Tenancy:**
- Two-level tenant isolation: Site (primary customer) → Client (customer's customers)
- Sites are isolated from each other
- Clients within a site are isolated from each other
- Role and permission system controls access for staff and client users

**Localization:**
- Uses GetText functionality and localization tables in DB
- This approach must be preserved in modernized solution

**Legacy Considerations:**
- Original solution has significant dead code in both codebase and database schema
- Migration must be selective — don't blindly port everything
- Page-by-page or module-by-module incremental approach

**Database Schema Documentation:**
- Comprehensive schema docs available at: `C:\Users\it\Documents\Dev\CustomersAndProjects\Signatur\Dev3\SigDB.MD`

## Constraints

- **Zero Downtime**: Cannot disrupt production system — requires careful rollout strategy with rollback capability via YARP routing
- **Database Immutability**: Cannot alter existing database schema — only add new tables if absolutely necessary, all additions must be scripted for migration
- **Tech Stack**: .NET 10, ASP.NET Core, Blazor, MudBlazor, EF Core, TUnit (unit tests), Playwright (E2E tests), Scalar (OpenAPI)
- **Architecture**: Clean architecture with clear layer separation, repository pattern, Unit of Work pattern, DTO pattern, no AutoMapper
- **Performance**: Must match or improve on original solution — performance is critical
- **Maintainability**: 10+ year lifecycle — prioritize long-term maintainability over short-term convenience
- **UI Fidelity**: Match original look and feel closely (not pixel-perfect but recognizable) — Blazor/MudBlazor under the hood
- **Testing**: Unit tests and E2E tests are mandatory part of workflow — not optional
- **Source Control**: Local git + remote GitHub, commit frequently on meaningful progress

## Key Decisions

| Decision | Rationale | Outcome |
|----------|-----------|---------|
| Start with E-recruitment portal | Core value is the activity list — ATS is central to platform value | — Pending |
| Core workflow only for v1 | Activity CRUD + application viewing provides functional baseline before advanced features | — Pending |
| YARP + System.Web Adapters | Enables incremental migration with zero downtime and shared session/auth | — Pending |
| Clean architecture from day one | 10+ year maintainability requirement demands proper separation of concerns | — Pending |
| EF Core (not LLBLGen) | Modern ORM, avoid porting old ORM code and patterns | — Pending |
| Repository + DTO patterns | Encapsulate data access, manual mapping for clarity and control | — Pending |
| Playwright E2E tests | Verify functional equivalence between original and modernized solutions | — Pending |
| MudBlazor components | Accelerate UI development while maintaining Blazor flexibility | — Pending |
| Preserve database schema | Zero risk to production data, maintain backward compatibility | — Pending |

---
*Last updated: 2026-02-13 after initialization*
