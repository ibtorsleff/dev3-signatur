# Feature Research: WebForms to Blazor Migration Features & Phasing

**Domain:** ASP.NET WebForms ATS (Applicant Tracking System) modernization to Blazor
**Researched:** 2026-02-13
**Confidence:** HIGH (Microsoft official docs + established migration patterns)

## Migration Phasing Strategy

This is a **Strangler Fig migration**, not a greenfield feature build. Feature "development" means migrating existing functionality into a modern stack while both systems run in parallel. The phasing must respect infrastructure dependencies — you cannot migrate a page until its cross-cutting concerns (auth, session, navigation) work.

### Phase Model

```
Phase 0: Infrastructure Shell (YARP + auth + session + nav)
    |
Phase 1: Core Read Views (activity list + application viewing)
    |
Phase 2: Core Write Operations (activity CRUD)
    |
Phase 3: Supporting Features (hiring team, roles, permissions, localization)
    |
Phase 4: Polish & Cutover (E2E tests, performance, UI parity)
    |
[Future: Other portals]
```

**Rationale:** Infrastructure must exist before any page works. Read views are lower risk than write operations. Supporting features can be added incrementally once the core workflow is functional.

---

## Feature Landscape

### Phase 0 — Infrastructure (Table Stakes for Everything Else)

Features that enable ALL subsequent migration. Nothing else works without these.

| Feature | Why Required | Complexity | Depends On | Notes |
|---------|-------------|------------|------------|-------|
| YARP reverse proxy | Routes requests between WebForms and Blazor apps; enables zero-downtime migration | MEDIUM | Nothing — first thing built | New ASP.NET Core app becomes the entry point, proxies unhandled routes to legacy WebForms app |
| System.Web Adapters (session) | Shares session state between old and new apps so user context survives routing between them | HIGH | YARP operational | Uses remote app session pattern; ASP.NET Framework serializes differently than Core (objects vs byte[]) |
| System.Web Adapters (auth) | Shares authentication state so users stay logged in across both apps | HIGH | YARP operational | Uses remote authentication; delegates auth decisions to legacy app during transition |
| Navigation shell / layout | Top navigation bar matching original UI; provides consistent frame for migrated pages | MEDIUM | YARP + auth working | MudBlazor layout with AppBar; must match existing nav structure and menu items |
| Clean architecture scaffolding | Project structure: Domain, Application, Infrastructure, Web layers | LOW | Nothing — structural decision | Influences every subsequent file placement; set up once, never redo |
| EF Core + DbContext setup | Database access layer pointing at existing SignaturAnnoncePortal database | MEDIUM | Clean architecture structure | Map to existing tables (no schema changes); DbContextFactory pattern for Blazor Server |
| Configuration migration | Move relevant web.config settings to appsettings.json + IConfiguration | LOW | ASP.NET Core project exists | Connection strings, feature flags, tenant config |
| DI container setup | Register all services in ASP.NET Core's built-in DI | LOW | Clean architecture structure | Replace Autofac/manual instantiation with built-in DI |
| TUnit test framework | Unit test project wired up and running | LOW | Solution structure exists | Parallel to feature work; not blocking |
| Playwright E2E framework | Browser test project configured against both legacy and modern apps | MEDIUM | Both apps runnable | Can start with smoke tests; expand with features |

### Phase 1 — Core Read Views (First User-Facing Value)

The activity list is the "heart of the ATS" and the core value proposition. Migrate read-only views first because they are lower risk (no data mutation) and provide the highest visibility for validation.

| Feature | Why Expected | Complexity | Depends On | Notes |
|---------|-------------|------------|------------|-------|
| Activity list view | Main view of the E-recruitment portal; users live in this view | HIGH | EF Core, auth, session, nav shell, multi-tenancy (read) | Most complex single component; filterable, sortable, paginated list of recruitment activities with status display. Use MudDataGrid. |
| Activity detail view (read) | Users click an activity to see its details | MEDIUM | Activity list view, EF Core | Detail page showing activity properties, status, metadata |
| Application viewing | View CVs and application letters for an activity | MEDIUM | Activity detail view, file access layer | Read-only display of candidate applications; may involve binary file retrieval from BinaryFile/ERCandidateFile tables |
| Hiring team display | Show team members assigned to an activity | LOW | Activity detail view, User table access | Read-only list of team members with roles |

### Phase 2 — Core Write Operations (Full CRUD Capability)

Once read views are validated, add write operations. These carry more risk because they mutate production data.

| Feature | Why Expected | Complexity | Depends On | Notes |
|---------|-------------|------------|------------|-------|
| Activity creation | Create a new recruitment activity | HIGH | Phase 1 complete, form validation, permission checks | Complex form with multiple fields, template selection, status initialization. Use MudBlazor EditForm/FluentValidation. |
| Activity editing | Update an existing recruitment activity | HIGH | Activity creation (shares form), optimistic concurrency | Same form as creation but with existing data loaded; must handle concurrent edits |
| Activity deletion | Remove a recruitment activity | LOW | Permission checks, activity detail view | Soft delete likely; need to understand existing deletion semantics from legacy app |
| Application storage | Persist new/modified application data | MEDIUM | EF Core write paths, file upload handling | Ensure candidate applications are properly stored; integrates with ERApplicationTemplateFieldData (5.8M rows — performance critical) |

### Phase 3 — Supporting Features (Cross-Cutting Capabilities)

These features span the entire application. They can be built incrementally — start with basic implementations and refine.

| Feature | Why Expected | Complexity | Depends On | Notes |
|---------|-------------|------------|------------|-------|
| Multi-tenancy (Site/Client isolation) | Two-level tenant isolation is fundamental to the platform | HIGH | EF Core, auth | Global query filters on DbContext; tenant resolved from session/auth context. Site -> Client hierarchy. Every query must be tenant-scoped. |
| Role and permission enforcement | Staff vs client user access controls | MEDIUM | Auth working, multi-tenancy | Map existing aspnet_Roles + Permission tables to Blazor `[Authorize]` attributes and `AuthorizeView` components |
| Localization (GetText + DB) | Multi-language UI matching original approach | MEDIUM | Database access, IStringLocalizer | Custom IStringLocalizer that reads from localization DB tables instead of .resx files; must match existing GetText key patterns |
| Audit logging | User activity tracking (UserActivityLog table) | LOW | Auth, multi-tenancy | Write to existing UserActivityLog table; middleware or service-level interception |
| Error handling | Consistent error pages and logging | LOW | ASP.NET Core middleware | Global exception handler, error boundaries in Blazor components |

### Differentiators (Modernization Benefits)

Features that improve on the original, made possible by the migration.

| Feature | Value Proposition | Complexity | Notes |
|---------|-------------------|------------|-------|
| Real-time UI updates (SignalR) | Activity status changes reflected immediately without page refresh | LOW | Blazor Server already uses SignalR; extend to push updates |
| Async data loading | Non-blocking UI during data fetch; loading states | LOW | Built into Blazor lifecycle (OnInitializedAsync); huge UX improvement over WebForms postbacks |
| Component reusability | Shared components across all four portals when they migrate | MEDIUM | Clean architecture + Blazor component model makes this natural |
| Modern form validation | Client-side + server-side validation with FluentValidation | LOW | Replace ASP.NET RequiredFieldValidator with DataAnnotations/FluentValidation |
| API-first data layer | RESTful endpoints for future integrations | MEDIUM | Clean architecture service layer naturally exposes as API; add Scalar/OpenAPI |
| Improved performance | EF Core + async + no ViewState overhead | MEDIUM | Measured against baseline from legacy app |

### Anti-Features (Do NOT Build Now)

Features that seem appealing but create problems or are out of scope.

| Feature | Why Requested | Why Problematic | Alternative |
|---------|---------------|-----------------|-------------|
| Full ASP.NET Core Identity migration | Replace aspnet_Membership with ASP.NET Core Identity | Requires password reset for all users; massive data migration risk; unnecessary during incremental migration | Use System.Web Adapters remote auth; defer identity migration until ALL portals are migrated |
| Database schema changes | "Clean up" the 455-table schema | Breaks legacy app that still serves three other portals; violates zero-downtime constraint | Read from existing schema as-is; add new tables only if absolutely necessary |
| Blazor WebAssembly | "Modern" client-side rendering | Adds API layer complexity; 6-7MB initial download; not appropriate for data-heavy internal portal | Blazor Server — more natural evolution from WebForms, server-side rendering |
| Migrate all four portals at once | "Complete the migration faster" | Massively increases scope, risk, and coordination burden | E-recruitment portal only; validate the approach, then repeat for other portals |
| Custom authentication system | "Build our own auth" | Security risk; maintenance burden; reinventing the wheel | Rely on System.Web Adapters during migration, then evaluate ASP.NET Core Identity post-migration |
| Real-time collaboration | Multiple users editing same activity simultaneously | Adds SignalR complexity, conflict resolution, and UI complexity far beyond current needs | Optimistic concurrency on save is sufficient; add real-time later if validated need exists |
| Advanced screening workflows | AI scoring, automated screening pipelines | Out of scope for v1; adds domain complexity before core CRUD is validated | Defer to v2 after core workflow is solid |
| Interview management | Calendar integration, scheduling, video links | Out of scope for v1; significant third-party integration complexity | Defer to v2 |

---

## Feature Dependencies

```
[YARP Reverse Proxy]
    |
    +--requires--> [System.Web Adapters (Session)]
    |                  |
    |                  +--requires--> [Multi-tenancy Read] (tenant from session)
    |                  |
    +--requires--> [System.Web Adapters (Auth)]
    |                  |
    |                  +--requires--> [Role/Permission Enforcement]
    |                  |
    +--requires--> [Navigation Shell]
                       |
                       +--requires--> [Activity List View]
                                          |
                                          +--requires--> [Activity Detail View]
                                          |                  |
                                          |                  +--requires--> [Application Viewing]
                                          |                  |
                                          |                  +--requires--> [Hiring Team Display]
                                          |
                                          +--requires--> [Activity Creation]
                                          |                  |
                                          |                  +--requires--> [Activity Editing] (shares form)
                                          |
                                          +--requires--> [Activity Deletion]

[EF Core + DbContext]
    |
    +--requires--> [Clean Architecture Scaffolding]
    |
    +--required by--> [Activity List View]
    +--required by--> [Multi-tenancy] (global query filters)
    +--required by--> [Localization] (DB-driven strings)

[Multi-tenancy]
    +--required by--> ALL data access (every query must be tenant-scoped)

[Localization]
    +--enhances--> ALL UI components (but NOT a hard blocker — can launch English-only)
```

### Dependency Notes

- **YARP requires nothing** and is the absolute first thing to build. It is the foundation of the Strangler Fig pattern.
- **Session and auth sharing require YARP** because the System.Web Adapters remote app pattern needs the Core app to be the entry point proxying to the Framework app.
- **Multi-tenancy requires session** because the current tenant context (Site/Client) comes from the user's session in the legacy app.
- **Activity list requires almost everything** — auth, session, multi-tenancy, EF Core, nav shell — because it is a tenant-scoped, permission-checked, database-driven view.
- **Localization enhances but does not block** — the system can launch English-only (or with hardcoded strings) and add localization as a refinement. However, since the existing system is localized, full parity requires it.

---

## Migration Phasing Definition

### Phase 0: Infrastructure Shell (Launch With)

The minimum to route a single Blazor page while legacy app handles everything else.

- [x] YARP reverse proxy routing between Blazor and WebForms apps
- [x] System.Web Adapters session sharing (remote app pattern)
- [x] System.Web Adapters authentication sharing (remote auth)
- [x] Navigation shell (top nav matching legacy UI)
- [x] Clean architecture project structure
- [x] EF Core DbContext mapped to existing database
- [x] Configuration (appsettings.json, connection strings)
- [x] DI container with service registrations
- [x] TUnit test project scaffolded
- [x] Playwright test project scaffolded

**Exit criteria:** A single "hello world" Blazor page serves behind YARP, user is authenticated via legacy app, session is shared, navigation works.

### Phase 1: Core Read Views (First User Value)

Users can view their recruitment activities and applications in the new UI.

- [ ] Activity list view — filterable, sortable, paginated (MudDataGrid)
- [ ] Activity detail view — read-only display of activity properties
- [ ] Application viewing — display candidate CVs and letters
- [ ] Hiring team display — show assigned team members
- [ ] Multi-tenancy read path — queries scoped to Site/Client
- [ ] Basic role checks — only authorized users see their activities

**Exit criteria:** Users can navigate to the E-recruitment portal, see their activity list, click into an activity, view applications and hiring team. All data is correct and tenant-isolated.

### Phase 2: Core Write Operations (Full CRUD)

Users can create, edit, and delete recruitment activities.

- [ ] Activity creation form — full form with validation
- [ ] Activity editing — load existing, modify, save
- [ ] Activity deletion — with confirmation and permission check
- [ ] Application storage — persist application data correctly
- [ ] Form validation — DataAnnotations or FluentValidation
- [ ] Optimistic concurrency — handle concurrent edits

**Exit criteria:** Full CRUD cycle works. User can create an activity, edit it, add applications, and delete it. All mutations are persisted correctly in the existing database.

### Phase 3: Supporting Features (Parity Refinement)

Bring the migrated portal to feature parity with the legacy version.

- [ ] Full role and permission enforcement — matching legacy permission model
- [ ] Localization — GetText + DB-driven string lookup via custom IStringLocalizer
- [ ] Audit logging — write to UserActivityLog table
- [ ] Error handling — global exception handler, error boundaries
- [ ] Performance optimization — query tuning for high-volume tables (ERApplicationTemplateFieldData: 5.8M rows, ERCandidate: 1.3M rows)

**Exit criteria:** E-recruitment portal in Blazor is functionally equivalent to the legacy version. All roles, permissions, languages, and audit trails work identically.

### Phase 4: Validation & Cutover (Production Readiness)

Ensure the migrated portal is production-ready and switch live traffic.

- [ ] Playwright E2E test suite — covering all core workflows
- [ ] Performance benchmarks — response times match or beat legacy
- [ ] UI parity verification — visual comparison with legacy
- [ ] Rollback procedure tested — YARP can route back to legacy instantly
- [ ] Load testing — concurrent users, large datasets
- [ ] Monitoring and alerting — health checks, error tracking

**Exit criteria:** E-recruitment portal fully operational on Blazor in production. YARP routing points all E-recruitment traffic to Blazor. Legacy app still serves other three portals unchanged.

### Future Phases (Not in Scope)

- [ ] Job advertisement portal migration — repeat Phase 0-4 pattern
- [ ] Onboarding/Offboarding portal migration
- [ ] Job bank portal migration
- [ ] ASP.NET Core Identity migration — replace aspnet_Membership after all portals migrated
- [ ] Advanced screening workflows
- [ ] Interview management
- [ ] Database schema modernization — only after legacy app fully decommissioned

---

## Feature Prioritization Matrix

| Feature | User Value | Implementation Cost | Risk | Priority |
|---------|-----------|-------------------|------|----------|
| YARP reverse proxy | None (invisible) | MEDIUM | HIGH (foundational) | P0 |
| System.Web Adapters (session) | None (invisible) | HIGH | HIGH (foundational) | P0 |
| System.Web Adapters (auth) | None (invisible) | HIGH | HIGH (foundational) | P0 |
| Navigation shell | HIGH | MEDIUM | LOW | P0 |
| Clean architecture scaffolding | None (invisible) | LOW | LOW | P0 |
| EF Core setup | None (invisible) | MEDIUM | MEDIUM | P0 |
| Activity list view | **CRITICAL** | HIGH | MEDIUM | P1 |
| Activity detail view | HIGH | MEDIUM | LOW | P1 |
| Application viewing | HIGH | MEDIUM | MEDIUM | P1 |
| Hiring team display | MEDIUM | LOW | LOW | P1 |
| Multi-tenancy | **CRITICAL** | HIGH | HIGH | P1 |
| Activity creation | HIGH | HIGH | MEDIUM | P2 |
| Activity editing | HIGH | HIGH | MEDIUM | P2 |
| Activity deletion | MEDIUM | LOW | LOW | P2 |
| Role/permission enforcement | HIGH | MEDIUM | MEDIUM | P2 |
| Localization | HIGH | MEDIUM | LOW | P3 |
| Audit logging | MEDIUM | LOW | LOW | P3 |
| Performance optimization | HIGH | MEDIUM | MEDIUM | P3 |
| Playwright E2E tests | HIGH | MEDIUM | LOW | P3 |

**Priority key:**
- P0: Infrastructure — must exist before any feature works
- P1: First user-facing value — validates the migration approach
- P2: Complete the core workflow — CRUD operations
- P3: Parity and polish — production readiness

---

## Zero-Downtime Alignment

The entire phasing strategy is designed around the zero-downtime constraint:

1. **YARP as traffic router:** The new Blazor app becomes the entry point on day one. All unmatched routes proxy to legacy. This means the legacy app continues serving ALL traffic until a Blazor route explicitly handles a request.

2. **Route-by-route migration:** Each migrated page is a new route in Blazor. When it's ready, YARP stops proxying that route and Blazor handles it directly. Rollback = re-enable the proxy for that route.

3. **Shared state:** Session and auth sharing via System.Web Adapters means users don't notice the boundary. They might view the activity list in Blazor, then click into a sub-feature still in WebForms, and the experience is seamless.

4. **No database changes:** By reading the existing schema as-is, the legacy app is never affected. Both apps can coexist indefinitely.

5. **Instant rollback:** At any point, YARP configuration can route ALL traffic back to legacy. The Blazor app is additive, never destructive.

---

## Sources

- [Microsoft Learn: Migrate from ASP.NET Web Forms to Blazor](https://learn.microsoft.com/en-us/dotnet/architecture/blazor-for-web-forms-developers/migration) — HIGH confidence, official Microsoft documentation
- [Microsoft Learn: Get started with incremental ASP.NET to ASP.NET Core migration](https://learn.microsoft.com/aspnet/core/migration/fx-to-core/start?view=aspnetcore-10.0) — HIGH confidence, official migration guide
- [Microsoft Learn: System.Web Adapters](https://learn.microsoft.com/aspnet/core/migration/fx-to-core/inc/systemweb-adapters?view=aspnetcore-10.0) — HIGH confidence, official docs
- [Microsoft Learn: Session migration strategies](https://learn.microsoft.com/aspnet/core/migration/fx-to-core/areas/session?view=aspnetcore-10.0) — HIGH confidence, official docs
- [Microsoft Learn: Authentication migration strategies](https://learn.microsoft.com/aspnet/core/migration/fx-to-core/areas/authentication?view=aspnetcore-10.0) — HIGH confidence, official docs
- [Microsoft Learn: Security, Authentication and Authorization in WebForms and Blazor](https://learn.microsoft.com/dotnet/architecture/blazor-for-web-forms-developers/security-authentication-authorization) — HIGH confidence, official eBook
- [Microsoft .NET Blog: Incremental ASP.NET to ASP.NET Core Migration](https://devblogs.microsoft.com/dotnet/incremental-asp-net-to-asp-net-core-migration/) — HIGH confidence, official blog
- [Trailhead Technology: Upgrading WebForms to Blazor with YARP](https://trailheadtechnology.com/upgrading-an-asp-net-web-forms-app-to-blazor-incrementally-with-yarp/) — MEDIUM confidence, reputable partner
- [Telerik: How to Migrate Web Forms App to Blazor in 6 Steps](https://www.telerik.com/blogs/how-to-migrate-web-forms-app-blazor-6-steps) — MEDIUM confidence, established vendor
- [Devox Software: Modernizing ASP.NET WebForms 2025 Engineering Playbook](https://devoxsoftware.com/blog/modernizing-asp-net-webforms-a-2025-engineering-playbook/) — MEDIUM confidence, industry analysis
- [Softacom: Migrating from ASP.NET WebForms to Blazor Complete Guide](https://www.softacom.com/wiki/migration/migrating-from-asp-net-webforms-to-blazor-a-complete-guide/) — MEDIUM confidence, migration guide
- [MudBlazor: DataGrid Component](https://mudblazor.com/components/datagrid) — HIGH confidence, official component docs
- [Microsoft Learn: Multi-tenancy with EF Core](https://learn.microsoft.com/en-us/ef/core/miscellaneous/multitenancy) — HIGH confidence, official docs

---
*Feature research for: SignaturPortal WebForms to Blazor modernization*
*Researched: 2026-02-13*
