# Requirements: SignaturPortal E-Recruitment Modernization

**Defined:** 2026-02-13
**Core Value:** The activity list view must work perfectly. Users must be able to see and navigate their recruitment activities with correct status.

## v1 Requirements

Requirements for E-recruitment portal core workflow migration (Phase 1-6).

### Infrastructure Foundation

- [ ] **INFRA-01**: YARP reverse proxy routes requests between Blazor and WebForms apps with explicit route ordering
- [ ] **INFRA-02**: System.Web Adapters remote session sharing operational between old and new apps
- [ ] **INFRA-03**: System.Web Adapters remote authentication sharing operational between old and new apps
- [ ] **INFRA-04**: Navigation shell displays with top nav matching original UI structure
- [ ] **INFRA-05**: Clean Architecture project structure created (Domain, Application, Infrastructure, Web layers)
- [ ] **INFRA-06**: EF Core DbContext scaffolded from existing database with partial class support
- [ ] **INFRA-07**: Multi-tenancy global query filters enforce Site/Client isolation on all queries
- [ ] **INFRA-08**: IDbContextFactory configured for Blazor Server circuit-safe database access
- [ ] **INFRA-09**: Repository pattern interfaces defined in Domain layer
- [ ] **INFRA-10**: Unit of Work pattern implemented for transaction coordination
- [ ] **INFRA-11**: DTO extension methods created for manual mapping (ToDto, ProjectToDto)
- [ ] **INFRA-12**: TUnit test project scaffolded with initial smoke tests
- [ ] **INFRA-13**: Playwright test project scaffolded with browser automation setup
- [ ] **INFRA-14**: Configuration management (appsettings.json, connection strings, DI registration)

### Multi-Tenancy & Security

- [ ] **SEC-01**: Tenant context resolver (ITenantResolver) extracts Site/Client from session/auth
- [ ] **SEC-02**: DbContext query filters automatically scope queries to current tenant
- [ ] **SEC-03**: SaveChanges interceptor validates ClientId on all added/modified entities
- [ ] **SEC-04**: Cross-tenant integration tests verify data isolation
- [ ] **SEC-05**: Role-based authorization enforces aspnet_Roles checks on Blazor components
- [ ] **SEC-06**: Permission-based authorization enforces custom Permission table checks
- [ ] **SEC-07**: Authorization policies defined for E-recruitment portal access levels

### Activity List (Core Read Views)

- [ ] **ELIST-01**: Activity list view displays user's recruitment activities in MudDataGrid
- [ ] **ELIST-02**: Activity list supports filtering by status, date range, assigned user
- [ ] **ELIST-03**: Activity list supports sorting by any column
- [ ] **ELIST-04**: Activity list supports pagination with configurable page size
- [ ] **ELIST-05**: Activity list scoped to current tenant (Site/Client isolation verified)
- [ ] **ELIST-06**: Activity list only shows activities user has permission to view
- [ ] **ELIST-07**: Activity list performance matches or improves on legacy (load time < 2s for typical query)
- [ ] **ELIST-08**: Activity detail view displays read-only activity properties
- [ ] **ELIST-09**: Activity detail view shows hiring team members
- [ ] **ELIST-10**: Activity detail view displays linked applications with candidate info

### Application Viewing

- [ ] **APP-01**: Application list displays candidates for selected activity
- [ ] **APP-02**: Application detail view shows candidate CV and application letter
- [ ] **APP-03**: Application viewing respects file storage (database vs. filesystem)
- [ ] **APP-04**: Application attachments (CV, diplomas) are downloadable
- [ ] **APP-05**: Application list supports search by candidate name

### Activity CRUD (Core Write Operations)

- [x] **CRUD-01**: Activity creation form displays with all required fields
- [ ] **CRUD-02**: Activity creation validates input using FluentValidation
- [x] **CRUD-03**: Activity creation saves to database with correct tenant scoping
- [x] **CRUD-04**: Activity editing loads existing activity data
- [x] **CRUD-05**: Activity editing saves changes with optimistic concurrency handling
- [x] **CRUD-06**: Activity deletion requires confirmation dialog
- [ ] **CRUD-07**: Activity deletion checks permissions before allowing delete
- [ ] **CRUD-08**: Activity deletion handles cascade deletes correctly
- [ ] **CRUD-09**: All write operations audit log to UserActivityLog table
- [ ] **CRUD-10**: Form auto-save prevents data loss on circuit disconnection

### Localization & UX

- [ ] **LOC-01**: Custom IStringLocalizer implementation reads from localization database tables
- [ ] **LOC-02**: GetText key patterns matched exactly from legacy implementation
- [ ] **LOC-03**: Danish localization works identically to legacy version
- [ ] **LOC-04**: Language selection persists in user session
- [ ] **UX-01**: Loading states display during async operations
- [ ] **UX-02**: Error messages match legacy text and positioning
- [ ] **UX-03**: Validation errors display in-line with form fields
- [ ] **UX-04**: Circuit reconnection UI displays custom message on disconnect
- [ ] **UX-05**: Circuit retention period configured for reasonable disconnect tolerance

### Testing & Verification

- [ ] **TEST-01**: Unit tests cover all Application layer use cases (TUnit)
- [ ] **TEST-02**: Integration tests verify repository operations with real database
- [ ] **TEST-03**: Playwright E2E tests cover activity list view workflow
- [ ] **TEST-04**: Playwright E2E tests cover activity CRUD workflow
- [ ] **TEST-05**: Playwright tests compare Blazor vs. WebForms visual equivalence
- [ ] **TEST-06**: Load tests verify performance under concurrent users
- [ ] **TEST-07**: Cross-tenant isolation tests verify no data leaks

### Deployment & Monitoring

- [ ] **DEPLOY-01**: YARP configuration changes apply without restart
- [ ] **DEPLOY-02**: Blazor app deployment doesn't break in-flight proxied requests
- [ ] **DEPLOY-03**: Rollback procedure tested (YARP routes back to legacy instantly)
- [ ] **DEPLOY-04**: Health checks configured for Blazor app and YARP proxy
- [ ] **MONITOR-01**: Serilog configured with structured logging
- [ ] **MONITOR-02**: Error tracking captures unhandled exceptions
- [ ] **MONITOR-03**: Performance metrics logged (page load times, query durations)

## v2 Requirements

Deferred to future milestones or subsequent portals.

### Other Portals

- **JOB-ADV**: Job advertisement portal migration
- **ONBOARD**: Onboarding/Offboarding portal migration
- **JOBBANK**: Job bank portal migration

### Advanced E-Recruitment Features

- **SCREEN-01**: Advanced screening workflow with customizable stages
- **INTERVIEW-01**: Interview scheduling and management
- **COLLAB-01**: Real-time collaboration on candidate evaluation
- **NOTIF-01**: Real-time notifications for activity updates

### Authentication Modernization

- **AUTH-MOD-01**: Migrate from System.Web Adapters to ASP.NET Core Identity
- **AUTH-MOD-02**: Password reset flow for all users during authentication cutover
- **AUTH-MOD-03**: MitID and Kombit integration migration to modern approach

### Database Modernization

- **DB-MOD-01**: Database schema optimization (after legacy decommissioned)
- **DB-MOD-02**: Remove unused tables and columns (dead code cleanup)

## Out of Scope

| Feature | Reason |
|---------|--------|
| Blazor WebAssembly | Adds API layer complexity; not appropriate for data-heavy internal portal with existing database |
| Database schema changes | Cannot alter existing schema (constraint); breaks legacy app; only after legacy decommissioned |
| Full ASP.NET Core Identity now | Requires password reset for all users; defer until all four portals migrated |
| Migrate all portals simultaneously | Massive scope increase; validate approach on E-recruitment first, then repeat pattern |
| Modern authentication (MitID/Kombit) now | Complex integration; defer until System.Web Adapters removed (after all portals migrated) |
| Real-time collaboration | SignalR complexity beyond current needs; e-recruitment is not collaborative editing |
| LLBLGen ORM migration | Use EF Core instead; don't port old ORM code and patterns forward |
| AutoMapper | Became commercial in 2025; use manual mapping or Mapperly source generator |

## Traceability

Which phases cover which requirements. Updated during roadmap creation.

| Requirement | Phase | Status |
|-------------|-------|--------|
| INFRA-01 | Phase 1 | Pending |
| INFRA-02 | Phase 1 | Pending |
| INFRA-03 | Phase 1 | Pending |
| INFRA-04 | Phase 1 | Pending |
| INFRA-05 | Phase 1 | Pending |
| INFRA-06 | Phase 1 | Pending |
| INFRA-07 | Phase 2 | Pending |
| INFRA-08 | Phase 1 | Pending |
| INFRA-09 | Phase 1 | Pending |
| INFRA-10 | Phase 1 | Pending |
| INFRA-11 | Phase 1 | Pending |
| INFRA-12 | Phase 1 | Pending |
| INFRA-13 | Phase 1 | Pending |
| INFRA-14 | Phase 1 | Pending |
| SEC-01 | Phase 2 | Pending |
| SEC-02 | Phase 2 | Pending |
| SEC-03 | Phase 2 | Pending |
| SEC-04 | Phase 2 | Pending |
| SEC-05 | Phase 2 | Pending |
| SEC-06 | Phase 2 | Pending |
| SEC-07 | Phase 2 | Pending |
| ELIST-01 | Phase 3 | Pending |
| ELIST-02 | Phase 3 | Pending |
| ELIST-03 | Phase 3 | Pending |
| ELIST-04 | Phase 3 | Pending |
| ELIST-05 | Phase 3 | Pending |
| ELIST-06 | Phase 3 | Pending |
| ELIST-07 | Phase 3 | Pending |
| ELIST-08 | Phase 3 | Pending |
| ELIST-09 | Phase 3 | Pending |
| ELIST-10 | Phase 3 | Pending |
| APP-01 | Phase 3 | Pending |
| APP-02 | Phase 3 | Pending |
| APP-03 | Phase 3 | Pending |
| APP-04 | Phase 3 | Pending |
| APP-05 | Phase 3 | Pending |
| CRUD-01 | Phase 4 | Complete |
| CRUD-02 | Phase 4 | Pending |
| CRUD-03 | Phase 4 | Complete |
| CRUD-04 | Phase 4 | Complete |
| CRUD-05 | Phase 4 | Complete |
| CRUD-06 | Phase 4 | Complete |
| CRUD-07 | Phase 4 | Pending |
| CRUD-08 | Phase 4 | Pending |
| CRUD-09 | Phase 4 | Pending |
| CRUD-10 | Phase 4 | Pending |
| LOC-01 | Phase 5 | Pending |
| LOC-02 | Phase 5 | Pending |
| LOC-03 | Phase 5 | Pending |
| LOC-04 | Phase 5 | Pending |
| UX-01 | Phase 5 | Pending |
| UX-02 | Phase 5 | Pending |
| UX-03 | Phase 5 | Pending |
| UX-04 | Phase 5 | Pending |
| UX-05 | Phase 5 | Pending |
| TEST-01 | Phase 6 | Pending |
| TEST-02 | Phase 6 | Pending |
| TEST-03 | Phase 6 | Pending |
| TEST-04 | Phase 6 | Pending |
| TEST-05 | Phase 6 | Pending |
| TEST-06 | Phase 6 | Pending |
| TEST-07 | Phase 6 | Pending |
| DEPLOY-01 | Phase 6 | Pending |
| DEPLOY-02 | Phase 6 | Pending |
| DEPLOY-03 | Phase 6 | Pending |
| DEPLOY-04 | Phase 6 | Pending |
| MONITOR-01 | Phase 6 | Pending |
| MONITOR-02 | Phase 6 | Pending |
| MONITOR-03 | Phase 6 | Pending |

**Coverage:**
- v1 requirements: 61 total
- Mapped to phases: 61
- Unmapped: 0

---
*Requirements defined: 2026-02-13*
*Last updated: 2026-02-13 after roadmap creation*
