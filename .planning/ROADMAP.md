# Roadmap: SignaturPortal E-Recruitment Modernization

## Overview

This roadmap delivers the incremental migration of the E-recruitment portal from ASP.NET WebForms to .NET 10 + Blazor Server. The migration follows a strict dependency chain: infrastructure shell (YARP proxy, session/auth sharing, multi-tenancy) must be validated before any UI migration begins. Read-only views validate the architecture end-to-end before write operations introduce mutation risk. Supporting features (localization, permissions, UX polish) are layered on after core CRUD is functional. The final phase hardens everything for production with comprehensive testing, deployment procedures, and monitoring.

## Phases

**Phase Numbering:**
- Integer phases (1, 2, 3): Planned milestone work
- Decimal phases (2.1, 2.2): Urgent insertions (marked with INSERTED)

Decimal phases appear between their surrounding integers in numeric order.

- [ ] **Phase 1: Infrastructure Shell** - YARP proxy, session/auth sharing, project structure, EF Core, and test scaffolding
- [ ] **Phase 2: Multi-Tenancy & Security Foundation** - Tenant isolation, role/permission enforcement, and cross-tenant verification
- [x] **Phase 3: Core Read Views** - Activity list, activity detail, application viewing, and hiring team display
- [ ] **Phase 4: Core Write Operations** - Activity CRUD with validation, concurrency, audit logging, and auto-save
- [ ] **Phase 5: Localization & UX Polish** - GetText localization, error handling, loading states, and circuit resilience
- [ ] **Phase 6: Testing, Deployment & Monitoring** - E2E tests, performance verification, deployment procedures, and production monitoring

## Phase Details

### Phase 1: Infrastructure Shell
**Goal**: The Blazor app runs behind YARP, shares session and auth with the legacy WebForms app, and has a working project structure with database access and test frameworks ready
**Depends on**: Nothing (first phase)
**Requirements**: INFRA-01, INFRA-02, INFRA-03, INFRA-04, INFRA-05, INFRA-06, INFRA-08, INFRA-09, INFRA-10, INFRA-11, INFRA-12, INFRA-13, INFRA-14
**Success Criteria** (what must be TRUE):
  1. A Blazor page serves behind YARP while all unmigrated routes proxy correctly to the legacy WebForms app
  2. User navigating from a legacy page to a Blazor page retains their authenticated session without re-login
  3. Navigation shell displays top nav matching the original UI structure, and links work across both apps
  4. EF Core can query the existing database and return data through a repository, with results mapped to DTOs
  5. TUnit and Playwright test projects compile and execute a passing smoke test
**Plans**: 4 plans

Plans:
- [ ] 01-01-PLAN.md -- Solution scaffold with Clean Architecture project structure and Blazor Server app
- [ ] 01-02-PLAN.md -- YARP reverse proxy, System.Web Adapters session/auth, and navigation shell
- [ ] 01-03-PLAN.md -- EF Core database-first scaffolding, Repository/UoW, and DTO mappings
- [ ] 01-04-PLAN.md -- TUnit and Playwright test project scaffolding with smoke tests

### Phase 2: Multi-Tenancy & Security Foundation
**Goal**: All data access is automatically scoped to the current tenant, and role/permission checks prevent unauthorized access to E-recruitment features
**Depends on**: Phase 1
**Requirements**: INFRA-07, SEC-01, SEC-02, SEC-03, SEC-04, SEC-05, SEC-06, SEC-07
**Success Criteria** (what must be TRUE):
  1. A user logged into Site A with Client B sees only data belonging to that Site/Client combination -- no other tenant's data appears
  2. A SaveChanges call that attempts to write an entity with a mismatched ClientId is rejected before hitting the database
  3. A user without the required role or permission is denied access to protected E-recruitment pages and sees an appropriate message
  4. Cross-tenant integration tests pass, proving that tenant isolation holds under concurrent access from different tenants
**Plans**: TBD

Plans:
- [ ] 02-01: TBD
- [ ] 02-02: TBD

### Phase 3: Core Read Views
**Goal**: Users can see and navigate their recruitment activities, view application details, and review hiring teams -- the core read-only value of the E-recruitment portal
**Depends on**: Phase 2
**Requirements**: ELIST-01, ELIST-02, ELIST-03, ELIST-04, ELIST-05, ELIST-06, ELIST-07, ELIST-08, ELIST-09, ELIST-10, APP-01, APP-02, APP-03, APP-04, APP-05
**Success Criteria** (what must be TRUE):
  1. User sees their recruitment activities in a paginated, sortable, filterable list that loads in under 2 seconds for a typical query
  2. User can click an activity to view its full details, including the assigned hiring team members
  3. User can view the list of candidates for an activity, open a candidate's CV and application letter, and download attachments
  4. Activity list and application data respect tenant isolation -- users only see data from their own Site/Client
  5. Activity list only shows activities the current user has permission to view based on their role
**Plans**: 4 plans

Plans:
- [x] 03-01-PLAN.md -- MudBlazor setup, EF Core entity scaffolding, domain enums, and status mapping helpers
- [x] 03-02-PLAN.md -- Application-layer DTOs, ActivityService, QueryableExtensions, and Activity List page with MudDataGrid
- [x] 03-03-PLAN.md -- Activity Detail page with hiring team display, [User] table scaffolding, and candidate summary
- [x] 03-04-PLAN.md -- Candidate List, Candidate Detail, and file download functionality

### Phase 4: Core Write Operations
**Goal**: Users can create, edit, and delete recruitment activities with full validation, concurrency handling, and audit trails -- completing the core CRUD workflow
**Depends on**: Phase 3
**Requirements**: CRUD-01, CRUD-02, CRUD-03, CRUD-04, CRUD-05, CRUD-06, CRUD-07, CRUD-08, CRUD-09, CRUD-10
**Success Criteria** (what must be TRUE):
  1. User can create a new recruitment activity by filling out a validated form, and it appears in their activity list after save
  2. User can edit an existing activity, and concurrent edits by another user are handled gracefully without silent data loss
  3. User can delete an activity after confirming a dialog, and the deletion respects permissions and cascades correctly
  4. All create/edit/delete operations are recorded in the UserActivityLog audit table
  5. Form data survives a brief network disconnection (circuit loss) because it is auto-saved
**Plans**: 4 plans

Plans:
- [ ] 04-01-PLAN.md -- FluentValidation setup, write DTOs (Create/Edit), validators, and ActivityResult type
- [ ] 04-02-PLAN.md -- EditedId concurrency token configuration and SaveChangesAsync audit logging override
- [ ] 04-03-PLAN.md -- ActivityService write methods (Create/Update/Delete) with permission checks and unit tests
- [ ] 04-04-PLAN.md -- Blazor UI: Create/Edit forms with Blazilla validation, Delete dialog, PersistentComponentState

### Phase 5: Localization & UX Polish
**Goal**: The E-recruitment portal displays in the correct language (Danish/English), handles errors gracefully, and provides smooth async loading and circuit resilience matching or exceeding the legacy experience
**Depends on**: Phase 4
**Requirements**: LOC-01, LOC-02, LOC-03, LOC-04, UX-01, UX-02, UX-03, UX-04, UX-05
**Success Criteria** (what must be TRUE):
  1. User selecting Danish sees all UI text in Danish, matching the exact same strings as the legacy WebForms version
  2. Language selection persists across page navigations and browser sessions
  3. Async operations display loading indicators, and errors show user-friendly messages matching legacy text and positioning
  4. Validation errors appear inline next to the offending form field, matching legacy behavior
  5. A circuit disconnection shows a custom reconnection message, and the circuit retention period allows reasonable reconnection time
**Plans**: 3 plans

Plans:
- [ ] 05-01-PLAN.md -- Localization infrastructure: LocalizationEntry entity, DbStringLocalizer, DbStringLocalizerFactory, cache warmer, culture middleware
- [ ] 05-02-PLAN.md -- Localize existing UI pages, MudBlazor localization bridge, navigation menu localization
- [ ] 05-03-PLAN.md -- UX polish: ErrorBoundary, reconnect modal localization/branding, circuit resilience configuration

### Phase 6: Testing, Deployment & Monitoring
**Goal**: The migrated E-recruitment portal is verified as production-ready with comprehensive tests, proven deployment procedures, and operational monitoring in place
**Depends on**: Phase 5
**Requirements**: TEST-01, TEST-02, TEST-03, TEST-04, TEST-05, TEST-06, TEST-07, DEPLOY-01, DEPLOY-02, DEPLOY-03, DEPLOY-04, MONITOR-01, MONITOR-02, MONITOR-03
**Success Criteria** (what must be TRUE):
  1. Playwright E2E tests cover the full activity list and CRUD workflows, and visual comparison confirms the Blazor version matches the legacy look and feel
  2. Unit tests cover all Application layer use cases and integration tests verify repository operations against a real database
  3. Load tests confirm the Blazor portal handles concurrent users without degradation, matching or improving on legacy performance
  4. YARP routing can be switched back to legacy instantly (rollback tested), and deploying the Blazor app does not break in-flight requests
  5. Structured logging, error tracking, and health checks are operational and capturing meaningful data
**Plans**: TBD

Plans:
- [ ] 06-01: TBD
- [ ] 06-02: TBD
- [ ] 06-03: TBD

## Progress

**Execution Order:**
Phases execute in numeric order: 1 -> 2 -> 3 -> 4 -> 5 -> 6

| Phase | Plans Complete | Status | Completed |
|-------|---------------|--------|-----------|
| 1. Infrastructure Shell | 4/4 | Complete | 2026-02-13 |
| 2. Multi-Tenancy & Security Foundation | 0/2 | Not started | - |
| 3. Core Read Views | 4/4 | Complete | 2026-02-15 |
| 4. Core Write Operations | 0/4 | Planned | - |
| 5. Localization & UX Polish | 0/3 | Planned | - |
| 6. Testing, Deployment & Monitoring | 0/3 | Not started | - |

---
*Roadmap created: 2026-02-13*
*Last updated: 2026-02-15*
