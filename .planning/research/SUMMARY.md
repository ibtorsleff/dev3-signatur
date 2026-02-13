# Project Research Summary

**Project:** AtlantaSignatur E-Recruitment Portal Modernization
**Domain:** ASP.NET WebForms to Blazor incremental migration (ATS/E-recruitment platform)
**Researched:** 2026-02-13
**Confidence:** HIGH

## Executive Summary

This is a Strangler Fig migration of a production ASP.NET WebForms (.NET Framework 4.8) ATS platform to .NET 10 + Blazor Server, using YARP reverse proxy for zero-downtime routing and System.Web Adapters for session/auth sharing during transition. The migration focuses initially on the E-recruitment portal (one of four portals in the system), with a 455-table database schema that cannot be altered and a requirement for zero user-facing downtime.

The recommended approach is infrastructure-first: establish YARP proxy, System.Web Adapters session/auth sharing, and multi-tenancy enforcement before migrating any UI. Then migrate read-only views (activity list, application viewing) to validate the architecture, followed by write operations (CRUD), and finally supporting features (localization, permissions). Each migrated route is served by Blazor; unmigrated routes fall through to the legacy WebForms app. The two applications coexist indefinitely until all routes are migrated.

The key risks are: (1) YARP routing collisions causing wrong pages to serve, (2) session/auth state failing to share across the proxy boundary, (3) multi-tenancy data leaks due to missing tenant filters, (4) porting dead code forward without usage audits, and (5) Blazor Server circuit state loss destroying user work. All are mitigable with explicit infrastructure validation, cross-tenant integration tests, usage analytics before migration scope is finalized, and auto-save patterns for forms.

## Key Findings

### Recommended Stack

The stack is optimized for incremental migration with long-term maintainability (10+ year target). .NET 10 LTS (supported through November 2028) is the correct foundation. Blazor Interactive Server (not WASM) preserves the server-rendered model of WebForms and avoids requiring a full API layer. YARP 2.3.0 and System.Web Adapters 2.3.0 are Microsoft's official migration tools. EF Core 10 with database-first scaffolding maps to the existing immutable schema. MudBlazor 8.15.0 provides Material Design components without licensing cost. Manual DTO mapping via extension methods (not AutoMapper, which became commercial in 2025) ensures compile-time safety. TUnit replaces legacy xUnit with source-generated, parallel-by-default testing.

**Core technologies:**
- .NET 10 SDK (10.0.x) — LTS release with three-year support window, foundation for all downstream libraries
- Blazor Interactive Server — Server-side rendering with SignalR, matches WebForms architectural model, direct database access
- YARP 2.3.0 — Reverse proxy enabling incremental migration, routes requests between Blazor and WebForms
- System.Web Adapters 2.3.0 — Session and auth sharing between ASP.NET Framework and ASP.NET Core during transition
- EF Core 10 — Database-first ORM scaffolding from existing schema, supports partial classes for extensions
- MudBlazor 8.15.0 — MIT-licensed Material Design component library, production-ready DataGrid
- Mapperly 4.3.1 — Source-generated object mapping (DTO to entity), zero runtime overhead, compile-time safe
- FluentValidation 12.1.1 — Strongly-typed validation rules, integrates with pipeline behaviors
- Serilog 4.3.1 — Structured logging with rich sink ecosystem
- Polly 8.6.5 — Resilience patterns for YARP proxy calls and external integrations
- TUnit 1.13.x — Modern source-generated test framework with parallel execution
- Playwright 1.58.0 — E2E browser testing for visual equivalence verification

**Critical version notes:**
- Visual Studio 2026 required (VS 2022 cannot target .NET 10)
- MudBlazor 8.15.0 is stable for .NET 8/9, works on .NET 10 with minor issues; upgrade to 9.0.0 stable when released
- MediatR 14.0.0 has dual-license (RPL-1.5 + commercial); verify AtlantaSignatur's eligibility for Community Edition or use Wolverine as alternative

### Expected Features

The migration follows a phasing model: Infrastructure (YARP + session + auth) → Core Read Views (activity list, detail, application viewing) → Core Write Operations (CRUD) → Supporting Features (multi-tenancy, permissions, localization) → Validation & Cutover. This is not greenfield development; it is porting existing functionality into a modern stack while both systems run in parallel.

**Must have (infrastructure table stakes):**
- YARP reverse proxy routing between Blazor and WebForms apps
- System.Web Adapters session sharing (remote app pattern)
- System.Web Adapters authentication sharing (remote auth)
- Navigation shell matching legacy UI structure
- Clean architecture project structure (Domain, Application, Infrastructure, Web)
- EF Core DbContext mapped to existing database (455 tables, immutable schema)
- Multi-tenancy enforcement (two-level: Site/Client isolation via global query filters)

**Must have (core user value):**
- Activity list view — filterable, sortable, paginated (MudDataGrid), tenant-scoped
- Activity detail view — read-only display of activity properties
- Application viewing — display candidate CVs and letters
- Activity creation, editing, deletion — full CRUD with validation and permission checks
- Role and permission enforcement — matching existing aspnet_Roles + Permission tables

**Should have (parity refinement):**
- Localization — GetText + database-driven string lookup via custom IStringLocalizer
- Audit logging — write to existing UserActivityLog table
- Hiring team display — show assigned team members
- Async data loading with loading states (UX improvement over WebForms postbacks)

**Defer (v2+ or other portals):**
- Full ASP.NET Core Identity migration (requires password reset for all users; defer until all portals migrated)
- Database schema changes (breaks legacy app; only after legacy decommissioned)
- Blazor WebAssembly (adds API layer complexity; not appropriate for data-heavy internal portal)
- Migrating all four portals at once (massive scope increase; validate approach on E-recruitment first)
- Real-time collaboration (SignalR complexity beyond current needs)
- Advanced screening workflows, interview management (domain complexity before core CRUD validated)

### Architecture Approach

Clean Architecture with four layers: Domain (entities, interfaces, zero dependencies), Application (use cases, CQRS handlers, DTOs, validation), Infrastructure (EF Core, System.Web Adapters, external services), Web (Blazor components, YARP config, DI composition root). Dependency rule: Web → Application → Domain ← Infrastructure. Domain depends on nothing; Infrastructure implements Domain interfaces.

**Major components:**
1. **YARP Strangler Fig Proxy** — ASP.NET Core app sits in front of legacy WebForms app; migrated routes served by Blazor, unmigrated routes proxied to legacy; enables zero-downtime incremental migration
2. **Multi-Tenancy Enforcement** — Two-level hierarchy (Site → Client) enforced via EF Core global query filters; tenant resolved from session/auth context; every query automatically scoped to current tenant
3. **Repository + Unit of Work over EF Core** — Thin abstraction providing testability and isolation from EF Core specifics; thin because EF Core already implements these patterns internally; justification is 10+ year maintainability target
4. **Manual DTO Mapping via Extension Methods** — Static extension methods (ToDto, ProjectToDto) for compile-time safe mapping; avoids AutoMapper's commercial licensing and runtime reflection overhead
5. **IDbContextFactory for Blazor Server** — Create/dispose DbContext per operation, not per circuit; avoids long-lived DbContext accumulating tracked entities and causing stale data
6. **System.Web Adapters Remote Session/Auth** — Shares session state and authentication between old and new apps; ASP.NET Core calls legacy app's session/auth endpoints to retrieve state

**Architectural patterns:**
- Strangler Fig migration: new app in front, gradually claims routes from legacy
- Database-first EF Core: scaffold from existing schema, use partial classes for customizations, never use migrations
- Global query filters for multi-tenancy: automatic WHERE clause on every query
- Circuit-scoped tenant provider for Blazor Server
- Defense in depth for tenant isolation: DbContext filters + SaveChanges interceptor + use case validation

### Critical Pitfalls

Research identified seven critical pitfalls that cause rewrites or production outages, plus patterns for technical debt, integration gotchas, and performance traps.

1. **YARP Routing Fallback Collision with Blazor** — Both YARP and Blazor act as fallback, causing wrong pages to serve. Avoid: Explicitly map all migrated routes with @page directives, set YARP fallback order to high number (low priority), maintain route inventory document.

2. **Session State Null in Blazor via System.Web Adapters** — Session is null in Blazor Server components during interactive rendering (SignalR circuits lack HTTP context). Avoid: Load session data during SSR, persist to component/circuit state for interactive use; register ALL session keys explicitly with serialization types; plan to eliminate session dependency as pages migrate.

3. **Porting Dead Code and Technical Debt Forward** — Migrating unused features consumes months of effort. Avoid: Instrument legacy app with page-level analytics for 4-6 weeks before migration; run static analysis to find dead code; create explicit "migration scope" document requiring business owner confirmation for each feature.

4. **Multi-Tenancy Isolation Broken During Migration** — Tenant data leaks or wrong tenant context persists in long-lived Blazor circuits. Avoid: Use IDbContextFactory (not scoped DbContext) in Blazor Server; implement ITenantResolver used by both apps; verify tenant isolation with explicit cross-tenant integration tests.

5. **Authentication Cookie/Redirect Loops Across YARP Boundary** — Infinite redirects when navigating between legacy and new pages. Avoid: Use System.Web Adapters remote authentication handler; configure UseForwardedHeaders middleware; set consistent cookie names/paths; test auth flows across proxy boundary.

6. **EF Core Scaffolding Fails on Legacy Schema** — Scaffold produces broken or incomplete entity models. Avoid: Use EF Core Power Tools (not CLI) for better edge case handling; scaffold incrementally (tables for first page, not entire database); use HasNoKey for views; use Fluent API for composite keys; use partial classes for customizations.

7. **Blazor Server Circuit State Loss Destroys User Work** — Users lose form data when SignalR circuit drops. Avoid: Implement auto-save for all forms; extend DisconnectedCircuitRetentionPeriod appropriately; customize reconnection UI; design components to be rehydratable from saved draft state.

**Additional patterns identified:**
- Technical debt: Never copy-paste code-behind into Blazor @code blocks; never use scoped DbContext in Blazor Server; never skip dead code analysis
- Integration: Forward X-Forwarded-* headers through YARP; register all session keys with consistent serialization; consolidate static assets to ASP.NET Core app
- Performance: Set circuit limits (MaxRetainedDisconnectedCircuits); use .Include() for eager loading; use IDbContextFactory; convert all I/O to async; use <Virtualize> for large lists
- Security: Legacy app should only accept connections from YARP (bind to localhost); store adapter API keys in secure storage; re-validate authorization on every Blazor component; validate tenant context from auth claims, never request headers

## Implications for Roadmap

Based on combined research, the migration should follow a strict dependency order: Infrastructure → Vertical Slice → Feature Migration. The critical path is: SharedKernel → Domain interfaces → Infrastructure DbContext with tenant filters → Web host with YARP → First vertical slice. This critical path must complete before any parallel feature work begins.

### Suggested Phase Structure

### Phase 0: Infrastructure Shell (Foundation)
**Rationale:** Nothing else works without these components. YARP, session/auth sharing, and multi-tenancy are hard dependencies for every subsequent page migration. This phase must be complete and validated before any UI migration begins.

**Delivers:**
- ASP.NET Core app with YARP reverse proxy routing all requests to legacy WebForms
- System.Web Adapters session sharing (remote app pattern) operational
- System.Web Adapters authentication sharing (remote auth) operational
- Navigation shell (top nav bar matching legacy UI)
- Clean architecture project structure (Domain, Application, Infrastructure, Web layers)
- EF Core DbContext scaffolded from existing database with global query filters for multi-tenancy
- Configuration (appsettings.json, connection strings)
- DI container with service registrations
- TUnit and Playwright test projects scaffolded

**Addresses Features:**
- YARP reverse proxy (infrastructure table stakes)
- System.Web Adapters session and auth (infrastructure table stakes)
- Navigation shell (infrastructure table stakes)
- Clean architecture scaffolding (infrastructure table stakes)
- EF Core setup (infrastructure table stakes)

**Avoids Pitfalls:**
- YARP routing collision: Validated with integration tests before any page migration
- Session state null: Plumbing established and tested end-to-end
- Multi-tenancy isolation: Global query filters implemented and verified with cross-tenant tests
- Auth redirect loops: Forwarded headers configured, tested across proxy boundary

**Exit Criteria:** A single "hello world" Blazor page serves behind YARP, user is authenticated via legacy app, session is shared, navigation works, tenant context resolves correctly.

**Research Flag:** Phase needs detailed technical validation. Use `/gsd:research-phase` to investigate YARP configuration edge cases and System.Web Adapters session serialization requirements specific to the SignaturAnnoncePortal database schema.

---

### Phase 1: Core Read Views (First User Value)
**Rationale:** Validate the architecture end-to-end with read-only views before attempting write operations. The activity list is the "heart of the ATS" and provides highest visibility for validation. Read operations are lower risk than writes (no data mutation) and provide immediate user-facing value to demonstrate progress.

**Delivers:**
- Activity list view (filterable, sortable, paginated using MudDataGrid)
- Activity detail view (read-only display of activity properties)
- Application viewing (display candidate CVs and letters)
- Hiring team display (show assigned team members)
- Multi-tenancy read path verified (queries scoped to Site/Client)
- Basic role checks (only authorized users see their activities)

**Uses Stack:**
- Blazor Interactive Server for components
- MudBlazor DataGrid for activity list
- EF Core with ProjectToDto() pattern for query optimization
- Repository pattern for data access
- Manual DTO mapping via extension methods

**Implements Architecture:**
- First vertical slice proving Domain → Application → Infrastructure → Web flow
- Repository implementations for ERActivity, ERCandidate entities
- MediatR query handlers (or simple Application services if deferring MediatR)
- Multi-tenancy enforcement validated end-to-end

**Avoids Pitfalls:**
- Dead code: Only implement features confirmed in usage analytics
- EF Core scaffolding failures: Scaffold only tables needed for this slice
- Circuit state loss: Read-only views don't have state loss risk (no forms yet)
- Performance traps: Use ProjectToDto() for large collections (ERApplicationTemplateFieldData: 5.8M rows)

**Exit Criteria:** Users can navigate to E-recruitment portal, see their activity list, click into an activity, view applications and hiring team. All data is correct and tenant-isolated. Playwright tests verify visual equivalence with legacy version.

**Research Flag:** Standard patterns (CRUD list views, MudBlazor DataGrid). Skip research-phase — rely on established patterns from STACK and ARCHITECTURE research.

---

### Phase 2: Core Write Operations (Full CRUD)
**Rationale:** Once read paths are validated, add write operations to complete the core workflow. Write operations carry more risk (data mutation, concurrency, validation) so they come after read validation. This phase delivers full CRUD capability, making the E-recruitment portal feature-complete for basic workflows.

**Delivers:**
- Activity creation form (full form with validation)
- Activity editing (load existing, modify, save)
- Activity deletion (with confirmation and permission check)
- Application storage (persist application data correctly)
- Form validation (FluentValidation integrated with MediatR pipeline behaviors or Blazor EditForm)
- Optimistic concurrency (handle concurrent edits via EF Core rowversion)

**Uses Stack:**
- FluentValidation for input validation
- MediatR command handlers (or Application services)
- Mapperly or manual mapping for command DTOs to entities
- Polly for resilience (if calling external services during save)
- MudBlazor forms (EditForm, input components)

**Implements Architecture:**
- Command handlers in Application layer
- Repository write methods (Add, Update, Delete)
- Unit of Work SaveChangesAsync coordination
- SaveChanges interceptor validates tenant ID on writes

**Avoids Pitfalls:**
- Circuit state loss: Implement auto-save pattern for all forms (save draft to database on significant changes)
- Multi-tenancy writes: SaveChanges interceptor validates ClientId on all added/modified entities
- Performance: Async all the way (no Task.Wait or Thread.Sleep from legacy code)
- UX downgrade: Match or exceed WebForms validation behavior (error message text and positioning)

**Exit Criteria:** Full CRUD cycle works. User can create an activity, edit it, add applications, and delete it. All mutations are persisted correctly. Concurrent edits handled gracefully. Form data survives circuit loss (auto-saved).

**Research Flag:** Standard CRUD patterns. Skip research-phase unless complex business rules emerge during implementation that require domain-specific investigation.

---

### Phase 3: Supporting Features (Parity Refinement)
**Rationale:** Core workflow (read + write) is functional. Now add cross-cutting features required for production parity: full permission model, localization, audit logging, performance optimization. These features span the entire application but can be built incrementally.

**Delivers:**
- Full role and permission enforcement (matching legacy aspnet_Roles + Permission tables)
- Localization (GetText + DB-driven string lookup via custom IStringLocalizer)
- Audit logging (write to existing UserActivityLog table)
- Error handling (global exception handler, Blazor error boundaries)
- Performance optimization (query tuning for high-volume tables, connection pooling, output caching)

**Uses Stack:**
- ASP.NET Core authorization middleware ([Authorize] attributes, policy-based authorization)
- Custom IStringLocalizer implementation reading from localization database tables
- Serilog for structured logging and audit events
- EF Core query optimization (AsNoTracking, Split queries for multiple includes)
- Polly for resilience and retry patterns

**Implements Architecture:**
- Permission checks in Application layer (use cases validate permissions before executing)
- Localization service in Infrastructure (implements IStringLocalizer interface)
- Audit interceptor in Infrastructure (logs to UserActivityLog on SaveChanges)
- Error boundary components in Web layer

**Avoids Pitfalls:**
- Performance traps: Address N+1 queries (use .Include()), ensure indexes exist for EF Core query patterns
- UX inconsistency: Localization matches existing GetText key patterns exactly
- Security: Re-validate authorization on every Blazor component (don't assume legacy checks transfer)

**Exit Criteria:** E-recruitment portal in Blazor is functionally equivalent to the legacy version. All roles, permissions, languages, and audit trails work identically. Performance matches or exceeds legacy app for typical workflows.

**Research Flag:** Localization implementation may need research-phase if GetText + database-driven pattern is non-standard. Permission model mapping may need domain-specific investigation.

---

### Phase 4: Validation & Cutover (Production Readiness)
**Rationale:** All features implemented; now verify production readiness with comprehensive testing, performance benchmarking, and cutover procedures. This phase ensures the migrated portal can handle production load and provides rollback capability.

**Delivers:**
- Playwright E2E test suite covering all core workflows
- Performance benchmarks (response times match or beat legacy)
- UI parity verification (visual comparison with legacy using Playwright screenshots)
- Rollback procedure tested (YARP can route back to legacy instantly)
- Load testing (concurrent users, large datasets)
- Monitoring and alerting (health checks, error tracking, Serilog sinks)

**Uses Stack:**
- Playwright 1.58.0 for E2E browser testing
- TUnit + Playwright for test execution
- Load testing tools (k6, NBomber, or Azure Load Testing)
- Application Insights or similar for production monitoring
- Serilog sinks for centralized logging (Seq, Elasticsearch, or Application Insights)

**Avoids Pitfalls:**
- "Looks done but isn't": Validate with production-like networking (separate hosts, HTTPS, load balancer), not just localhost
- Zero downtime deploy: Verify YARP configuration changes apply without restart; test that deploying ASP.NET Core app doesn't break in-flight proxied requests
- Dead code audit: Verify with production analytics (minimum 4 weeks) that no actively-used feature was excluded

**Exit Criteria:** E-recruitment portal fully operational on Blazor in production. YARP routing points all E-recruitment traffic to Blazor. Legacy app still serves other three portals unchanged. Rollback tested and documented.

**Research Flag:** Standard testing and deployment patterns. Skip research-phase.

---

### Phase Ordering Rationale

1. **Infrastructure-first is non-negotiable:** YARP, session/auth sharing, and multi-tenancy are hard dependencies. Attempting to migrate pages before these are validated results in rework.

2. **Read before write reduces risk:** Read-only views validate the architecture (Domain → Application → Infrastructure → Web flow, multi-tenancy, DTO mapping) without the risk of data corruption. Writing operations add validation, concurrency, and error handling complexity.

3. **Vertical slice proves architecture:** Phase 1 (Core Read Views) is an end-to-end vertical slice. It proves the Repository pattern works, EF Core scaffolding works, multi-tenancy works, MudBlazor integrates correctly, and Blazor components can render real data. This unblocks parallel feature work.

4. **Supporting features after core workflow:** Localization, permissions, and audit logging are important but not blockers for validating the migration approach. They can be added incrementally once core CRUD is functional.

5. **Validation phase catches production issues:** Comprehensive E2E testing, load testing, and production-like networking validation catch issues that unit/integration tests miss (circuit state loss on mobile networks, YARP header forwarding on load balancers, etc.).

### Dependency Chain

```
Phase 0 (Infrastructure)
    |
    +-- Enables --> Phase 1 (Core Read Views)
    |                  |
    |                  +-- Validates --> Architecture patterns used in Phase 2
    |                  |
    |                  +-- Unblocks --> Parallel feature work (if multiple teams)
    |
Phase 1 (Core Read Views)
    |
    +-- Requires --> Phase 2 (Core Write Operations)
    |                  |
    |                  +-- Delivers --> Full CRUD, making app usable
    |
Phase 2 (Core Write Operations)
    |
    +-- Enables --> Phase 3 (Supporting Features)
    |                  |
    |                  +-- Adds --> Cross-cutting concerns (localization, permissions)
    |
Phase 3 (Supporting Features)
    |
    +-- Leads to --> Phase 4 (Validation & Cutover)
                         |
                         +-- Delivers --> Production-ready portal
```

Multi-tenancy (Phase 0) is a hard dependency for all data access (Phase 1+). Session/auth sharing (Phase 0) is a hard dependency for any page migration. EF Core setup (Phase 0) is a hard dependency for read views (Phase 1).

### Research Flags by Phase

| Phase | Needs Research-Phase? | Reasoning |
|-------|----------------------|-----------|
| Phase 0: Infrastructure | YES | Complex integration (YARP + System.Web Adapters) with known edge cases. Session serialization requirements specific to SignaturAnnoncePortal schema may need investigation. Multi-tenancy implementation must be validated against existing Client/ClientSection hierarchy. |
| Phase 1: Core Read Views | NO | Standard CRUD list patterns. MudBlazor DataGrid is well-documented. EF Core scaffolding and query patterns are established. Proceed with confidence from STACK and ARCHITECTURE research. |
| Phase 2: Core Write Operations | NO | Standard CRUD write patterns. FluentValidation integration is well-documented. Auto-save pattern for circuit state loss is established in research. Optimistic concurrency via EF Core rowversion is standard. |
| Phase 3: Supporting Features | MAYBE | Localization (GetText + database-driven strings) may need research if pattern is non-standard. Permission model mapping from aspnet_Roles + custom Permission table may need domain investigation. Otherwise standard patterns. |
| Phase 4: Validation & Cutover | NO | Standard testing and deployment patterns. Playwright E2E testing is well-documented. Load testing tools are established. |

**Recommendation:** Use `/gsd:research-phase` for Phase 0 (Infrastructure) to investigate YARP configuration, System.Web Adapters session serialization, and multi-tenancy implementation details. Optionally use for Phase 3 if localization or permission model complexity emerges. Skip for Phases 1, 2, 4.

## Confidence Assessment

| Area | Confidence | Notes |
|------|------------|-------|
| Stack | HIGH | All core technologies have official Microsoft documentation (YARP, System.Web Adapters, EF Core, Blazor). Third-party libraries (MudBlazor, Mapperly, FluentValidation, Serilog, Polly) are mature and widely adopted. .NET 10 LTS provides three-year support window. MediatR licensing requires verification but Wolverine alternative exists. |
| Features | HIGH | Migration feature set is well-defined by existing WebForms functionality. Phase model (Infrastructure → Read → Write → Supporting → Validation) is validated by Microsoft's official incremental migration guidance and real-world case studies (Jimmy Bogard, Trailhead Technology). Usage analytics requirement addresses dead code risk. |
| Architecture | HIGH | Clean Architecture is Microsoft's recommended pattern (documented in eShopOnWeb, ardalis template). Repository + Unit of Work over EF Core is established for long-lived projects. Multi-tenancy via global query filters is official EF Core pattern. IDbContextFactory for Blazor Server is Microsoft-documented requirement. YARP Strangler Fig pattern is official migration approach. |
| Pitfalls | MEDIUM-HIGH | All seven critical pitfalls are confirmed by official Microsoft documentation, GitHub issues on dotnet/systemweb-adapters and dotnet/yarp repositories, or real-world migration case studies. Mitigation strategies are validated. Confidence is "medium-high" not "high" because every migration has unique edge cases; research provides patterns, not guarantees. |

**Overall confidence:** HIGH

The combination of official Microsoft documentation for all core technologies, established migration patterns from real-world case studies, and mature third-party libraries provides high confidence in the recommended approach. The main uncertainty is not "will this work" but "what edge cases will we encounter during implementation."

### Gaps to Address

**Gap: Exact session keys used by legacy WebForms app**
- Research identifies that ALL session keys must be registered explicitly with System.Web Adapters, but the specific keys used by the SignaturAnnoncePortal app are unknown.
- **How to handle:** During Phase 0, audit the legacy WebForms codebase to document all Session["key"] accesses. Create a single source-of-truth file listing all keys with their types. Test round-trip serialization for each key.

**Gap: Specific permission model implementation**
- Research confirms aspnet_Roles and a custom Permission table exist, but the exact permission check logic (how roles map to permissions, what the Permission table structure is) is unknown.
- **How to handle:** During Phase 3 (or earlier if needed for Phase 1 read views), reverse-engineer the legacy permission model. Document the mapping between roles, permissions, and UI elements. Implement identical checks in Blazor using [Authorize] attributes and policy-based authorization.

**Gap: Localization GetText key patterns**
- Research confirms a GetText + database-driven localization approach exists, but the exact key naming conventions and database table structure are unknown.
- **How to handle:** During Phase 3, audit the localization database tables (structure documented in SigDB.MD if available). Implement custom IStringLocalizer that matches existing key patterns exactly. Test with multiple languages (English, Danish at minimum).

**Gap: Database performance baseline**
- Research identifies high-volume tables (ERApplicationTemplateFieldData: 5.8M rows, ERCandidate: 1.3M rows) but actual query performance of the legacy app is unknown.
- **How to handle:** During Phase 0 or early Phase 1, establish performance baselines for key queries in the legacy app (page load times, query durations). Use as targets for Blazor implementation. Profile early and often.

**Gap: WebForms pages actually in use**
- Research emphasizes the importance of usage analytics to avoid porting dead code, but analytics instrumentation is not yet in place.
- **How to handle:** Immediately (before Phase 0 begins) instrument the legacy WebForms app with page-level request logging. Run for 4-6 weeks covering month-end and other periodic workflows. Use results to finalize migration scope.

**Gap: MediatR licensing eligibility**
- STACK research notes MediatR 14.0.0 has dual-license (RPL-1.5 + commercial) with Community Edition for companies under $5M revenue. AtlantaSignatur's eligibility is unknown.
- **How to handle:** Before Phase 0, verify company revenue and licensing eligibility. If ineligible or uncertain, use Wolverine (WolverineFx 5.x, MIT license) instead. Both provide similar CQRS/mediator patterns.

## Sources

All research files include comprehensive source lists with confidence ratings. Key sources:

### Primary (HIGH confidence)
- Microsoft Learn: ASP.NET Core 10 release notes, YARP documentation, System.Web Adapters guides, EF Core 10 documentation, Blazor fundamentals
- Microsoft DevBlogs: .NET 10 announcement, incremental migration guidance, System.Web Adapters releases
- Official NuGet package pages: Version verification for all recommended libraries
- GitHub repositories: dotnet/yarp, dotnet/systemweb-adapters, MudBlazor/MudBlazor, thomhurst/TUnit, riok/mapperly

### Secondary (MEDIUM confidence)
- Real-world migration case studies: Jimmy Bogard's "Tales from the .NET Migration Trenches" series (session state, authentication, cataloging)
- Vendor migration guides: Trailhead Technology YARP + Blazor guide, Devox Software WebForms modernization playbook
- Community expertise: Milan Jovanovic EF Core multi-tenancy, Jeremy Likness Blazor Server multi-tenancy
- GitHub issues: Confirmed pitfalls from dotnet/systemweb-adapters and dotnet/yarp issue trackers

### Tertiary (LOW confidence)
- None used for final recommendations. All tertiary sources from initial research were validated against primary/secondary sources or discarded.

### Source Quality Notes
- STACK research: 100% HIGH confidence sources (official Microsoft docs, NuGet verified versions)
- FEATURES research: HIGH confidence (Microsoft official migration guides + established migration patterns)
- ARCHITECTURE research: HIGH confidence (Microsoft Clean Architecture guidance + reference implementations)
- PITFALLS research: MEDIUM-HIGH confidence (all pitfalls confirmed by official docs or GitHub issues, but confidence is "medium-high" not "high" because specific edge cases may vary by project)

---

*Research completed: 2026-02-13*
*Ready for roadmap: Yes*
*Next step: Use SUMMARY.md as input for roadmap creation*
