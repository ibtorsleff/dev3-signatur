# Pitfalls Research

**Domain:** ASP.NET WebForms (.NET Framework 4.8) to .NET 10 + Blazor Modernization
**Researched:** 2026-02-13
**Confidence:** MEDIUM-HIGH (verified against Microsoft docs, GitHub issues, real-world migration case studies)

---

## Critical Pitfalls

Mistakes that cause rewrites, production outages, or multi-week setbacks.

### Pitfall 1: YARP Routing Fallback Collision with Blazor

**What goes wrong:**
Both YARP and Blazor attempt to act as the fallback route for unmatched requests. When not explicitly configured, requests meant for Blazor deep-linked pages get proxied to the legacy WebForms app (returning 404s or wrong pages), and vice versa. Users see broken navigation, blank pages, or the wrong version of the application.

**Why it happens:**
YARP's catch-all proxy route and Blazor's `MapFallbackToPage("/_Host")` both register as fallback endpoints. ASP.NET Core's routing system arbitrarily picks one when there is no explicit priority. This was a known issue in .NET 6/7, officially fixed in .NET 8+, but only if you configure route ordering correctly.

**How to avoid:**
- Explicitly map every migrated route in the Blazor app with `@page` directives before relying on fallback routing.
- Set YARP's fallback route order to a high number (low priority) so Blazor routes always win.
- In .NET 8+, use the corrected routing behavior but still verify with integration tests.
- Maintain a living route inventory document that tracks which routes are served by Blazor vs. proxied to WebForms.
- Use YARP's route matching configuration (`Match.Path` patterns) rather than catch-all for WebForms routes.

**Warning signs:**
- Deep links to Blazor pages return WebForms content or 404.
- Browser URL stays correct but content is from the wrong application.
- Intermittent navigation failures in Blazor that "fix themselves" on refresh.
- YARP access logs show requests being proxied that should be handled locally.

**Phase to address:**
Phase 1 (Infrastructure/Proxy Setup). Must be validated before any page migration begins. Re-validate each time a new route is migrated.

**Confidence:** HIGH (documented in [Microsoft Learn](https://learn.microsoft.com/aspnet/core/migration/fx-to-core/inc/blazor?view=aspnetcore-7.0), fixed in .NET 8+, confirmed by [Trailhead Technology](https://trailheadtechnology.com/upgrading-an-asp-net-web-forms-app-to-blazor-incrementally-with-yarp/))

---

### Pitfall 2: Session State Null in Blazor via System.Web Adapters

**What goes wrong:**
When sharing session state between WebForms and Blazor Server using `Microsoft.AspNetCore.SystemWebAdapters`, the session is always `null` on `HttpContext` in Blazor Server components. Authentication state may also fail to propagate, with `IsAuthenticated` returning `false` even for logged-in users.

**Why it happens:**
Blazor Server uses SignalR circuits, not traditional HTTP requests. The SignalR connection operates independently of an HTTP context, so session state (which requires an HTTP context) is not available during interactive Blazor rendering. The System.Web Adapters remote session mechanism requires an HTTP request/response cycle to the legacy app, which does not exist for SignalR-based component interactions. Additionally, all session keys must be explicitly registered with their serialization types in advance -- missing a single key causes silent failures.

**How to avoid:**
- Accept that session sharing only works during the initial HTTP request (SSR), not during interactive Blazor rendering.
- Design Blazor components to load session data during SSR and persist it to component/circuit state for interactive use.
- For auth state: use the remote authentication handler during initial page load, then maintain auth state in the circuit using `AuthenticationStateProvider`.
- Register ALL session keys explicitly with their `ISessionKeySerializer` implementations.
- Use the `RemoteAppSessionStateOptions` to set `CookieName` matching the legacy app's session cookie.
- Plan to eliminate session dependency entirely as pages migrate -- this is the correct long-term target.

**Warning signs:**
- Session values are null in Blazor components but work in SSR/Razor Pages.
- "Serialized session state has different version than expected" errors in logs.
- Intermittent 401s when navigating between legacy and new pages.
- Unregistered session key exceptions in production.

**Phase to address:**
Phase 1 (Infrastructure Setup) for the adapter plumbing. Phase 2 (First Page Migration) must validate session/auth flow end-to-end before migrating additional pages.

**Confidence:** HIGH (confirmed by [GitHub Issue #413](https://github.com/dotnet/systemweb-adapters/issues/413), [Issue #453](https://github.com/dotnet/systemweb-adapters/issues/453), [Issue #556](https://github.com/dotnet/systemweb-adapters/issues/556), [Jimmy Bogard's migration series](https://www.jimmybogard.com/tales-from-the-net-migration-trenches-session-state/))

---

### Pitfall 3: Porting Dead Code and Technical Debt Forward

**What goes wrong:**
Teams migrate WebForms pages, code-behind, and business logic 1:1 without auditing what is actually used. Dead features, obsolete workflows, and accumulated workarounds from years of maintenance get rebuilt in Blazor, consuming months of effort on code nobody uses. The new codebase inherits the old codebase's complexity without its reasons.

**Why it happens:**
Fear of breaking something. Without usage analytics on the legacy app, developers cannot distinguish active features from dead ones. The "safe" approach feels like porting everything. WebForms apps accumulate dead code particularly aggressively because code-behind files, user controls, and HTTP handlers are never tree-shaken.

**How to avoid:**
- Before migration begins, instrument the legacy WebForms app with page-level analytics (request counts per route, per user role) for at least 4-6 weeks.
- Run static analysis with ReSharper "Find dead code" or NDepend dependency analysis on the legacy solution.
- Use the .NET Portability Analyzer to identify API surface that cannot be ported (often correlates with dead code).
- Create a "migration scope" document: explicitly list pages/features that WILL be migrated, and pages that WILL NOT. Default is "do not migrate" -- features must earn inclusion.
- For each WebForms page, require a business owner to confirm it is actively used before scheduling migration.

**Warning signs:**
- Migration estimate keeps growing as developers discover "one more thing" that needs porting.
- No usage data exists for the legacy application.
- Migrated features have no test coverage because nobody can describe expected behavior.
- Developers are porting pages they have never seen a user access.

**Phase to address:**
Phase 0 (Discovery/Audit) -- before the migration roadmap is finalized. Usage data collection should start immediately.

**Confidence:** HIGH (universal pattern in legacy modernization projects, confirmed by [Devox Software](https://devoxsoftware.com/blog/modernizing-asp-net-webforms-a-2025-engineering-playbook/), [Jimmy Bogard's cataloging phase](https://www.jimmybogard.com/tales-from-the-net-migration-trenches/))

---

### Pitfall 4: Multi-Tenancy Isolation Broken During Migration

**What goes wrong:**
Tenant data leaks between tenants or tenant-specific configurations break when the same request is served by different applications (legacy vs. new). Connection string resolution, query filters, and tenant context do not propagate correctly across the YARP proxy boundary. In Blazor Server, the long-lived circuit scope causes the wrong tenant context to persist.

**Why it happens:**
WebForms resolves tenant context per-request (from URL, cookie, or session). When YARP proxies some requests to WebForms and handles others in Blazor, the tenant resolution mechanism must be consistent across both. Blazor Server's DI scope lives for the entire circuit duration, not per-request -- so a scoped `DbContext` configured for Tenant A could serve Tenant B if the tenant changes mid-circuit (unlikely but possible with shared browser sessions). EF Core global query filters for tenant isolation must be applied identically in both old and new data access layers.

**How to avoid:**
- Implement a single `ITenantResolver` service used by both the legacy adapter layer and the Blazor app.
- Register `DbContext` as Transient (not Scoped) in Blazor Server to ensure tenant context is re-evaluated per operation, or use `IDbContextFactory<T>`.
- Verify tenant isolation with explicit cross-tenant integration tests: create data as Tenant A, query as Tenant B, confirm zero results.
- Ensure YARP forwards all tenant-identifying headers/cookies to the legacy app.
- Apply EF Core global query filters for `TenantId` on every entity, and verify with a unit test that scans all entity types.

**Warning signs:**
- Data from one tenant appearing in another tenant's view (highest severity).
- Tenant-specific configuration (branding, features) showing incorrect values after navigation.
- `DbContext` connection string not changing when switching between tenants.
- Integration tests pass in single-tenant mode but fail in multi-tenant scenarios.

**Phase to address:**
Phase 1 (Infrastructure) for tenant resolution plumbing. Must be validated with cross-tenant tests before ANY data-accessing page is migrated.

**Confidence:** MEDIUM-HIGH (EF Core multi-tenancy patterns documented at [Microsoft Learn](https://learn.microsoft.com/en-us/ef/core/miscellaneous/multitenancy), Blazor-specific issues at [Jeremy Likness blog](https://blog.jeremylikness.com/blog/multitenancy-with-ef-core-in-blazor-server-apps/))

---

### Pitfall 5: Authentication Cookie/Redirect Loops Across YARP Boundary

**What goes wrong:**
Users experience infinite redirect loops when navigating between legacy WebForms pages and new Blazor pages. The authentication cookie set by one application is not recognized by the other, or redirect URLs point to internal hostnames instead of the public proxy URL.

**Why it happens:**
FormsAuthentication in WebForms and Cookie Authentication in ASP.NET Core use different cookie formats, encryption mechanisms, and validation logic. When YARP proxies a 302 redirect from the legacy app, the `Location` header contains the internal backend URL (e.g., `http://localhost:5001/login`) instead of the public-facing URL. Cookie domain/path mismatches prevent the browser from sending the auth cookie to the correct application. The `X-Forwarded-Host` and `X-Forwarded-Proto` headers must be explicitly configured and consumed by both apps.

**How to avoid:**
- Use System.Web Adapters' remote authentication handler so the ASP.NET Core app delegates all auth decisions to the legacy app during the migration period.
- Configure `UseForwardedHeaders` middleware in the ASP.NET Core app to trust YARP's `X-Forwarded-*` headers.
- Enable `AddProxySupport()` in the legacy WebForms app via System.Web Adapters.
- Set consistent cookie names and paths across both applications.
- Use a shared `DataProtectionProvider` if cookies need to be decrypted by both apps.
- Test authentication flows specifically across the proxy boundary: login on legacy, navigate to Blazor, and vice versa.

**Warning signs:**
- Browser shows multiple rapid redirects before an error page.
- Auth cookie present in browser DevTools but server returns 401.
- Login works within each app individually but breaks when crossing the proxy boundary.
- Redirect URLs in network tab show internal hostnames/ports.

**Phase to address:**
Phase 1 (Infrastructure). Authentication must work across the proxy boundary before any page migration. This is a gate criterion.

**Confidence:** HIGH (confirmed by [GitHub Issue #478](https://github.com/dotnet/systemweb-adapters/issues/478), [Issue #495](https://github.com/dotnet/systemweb-adapters/issues/495), [YARP Issue #2100](https://github.com/microsoft/reverse-proxy/issues/2100), [Microsoft docs](https://learn.microsoft.com/en-us/aspnet/core/migration/fx-to-core/areas/authentication?view=aspnetcore-10.0))

---

### Pitfall 6: EF Core Scaffolding Fails on Legacy Schema

**What goes wrong:**
`dotnet ef dbcontext scaffold` produces broken or incomplete entity models when run against the legacy database. Tables without primary keys are skipped. Composite keys are not mapped via data annotations. Computed columns, triggers, and views cause incorrect or missing mappings. The generated code requires extensive manual correction, and teams underestimate this effort.

**Why it happens:**
EF Core's scaffolding expects databases designed with EF Core conventions (single-column integer PKs, navigation properties via foreign keys, no triggers for business logic). Legacy databases designed for other ORMs (ADO.NET, NHibernate, custom DAL) or hand-written SQL often have: tables with no declared PK (using unique indexes instead), composite foreign keys with column name mismatches, computed columns using functions EF Core cannot parse, views that aggregate across schemas, stored procedures as the primary data access mechanism, and column types EF Core does not map cleanly (e.g., `sql_variant`, `hierarchyid`).

**How to avoid:**
- Use EF Core Power Tools (by ErikEJ) instead of the CLI for scaffolding -- it handles more edge cases and provides a GUI for selecting tables/views.
- Scaffold incrementally: start with the tables needed for the first migrated page, not the entire database.
- Use `HasNoKey()` for views and stored procedure result sets via keyless entity types.
- Use Fluent API (not data annotations) for all composite keys: `HasKey(e => new { e.Col1, e.Col2 })`.
- Never modify scaffolded files directly -- use partial classes for custom logic so re-scaffolding does not destroy customizations.
- Grant the scaffolding user `VIEW DEFINITION` rights to avoid missing default/computed column values.
- Set `CommandTimeout=300` in the connection string for databases with large index sets.
- Accept that stored procedures and complex views will require raw SQL or Dapper alongside EF Core -- do not force everything through DbContext.

**Warning signs:**
- Scaffolding command times out or produces warnings about skipped tables.
- Entity classes have no key properties for tables that clearly have data.
- Navigation properties are missing or incorrect for known relationships.
- Queries produce wrong results due to incorrect composite key mappings.

**Phase to address:**
Phase 1 (Data Access Layer Setup). Create a "database compatibility assessment" before attempting scaffolding.

**Confidence:** HIGH (confirmed by [ErikEJ's scaffolding gotchas](https://erikej.github.io/efcore/2020/10/12/ef-core-sqlserver-scaffolding-gotchas.html), [EF Core Power Tools wiki](https://github.com/ErikEJ/EFCorePowerTools/wiki/Reverse-Engineering), [Microsoft docs](https://learn.microsoft.com/en-us/ef/core/managing-schemas/scaffolding/))

---

### Pitfall 7: Blazor Server Circuit State Loss Destroys User Work

**What goes wrong:**
Users lose in-progress form data, unsaved changes, or their authenticated session when the SignalR circuit drops. This happens when: the user's device goes to sleep (mobile/laptop lid close), network connectivity briefly drops, the server redeploys, or the disconnected circuit retention period expires. Users see "Attempting to reconnect..." and then a full page reload that wipes all state.

**Why it happens:**
Blazor Server holds all component state in server memory tied to a specific circuit. When the circuit is lost, so is all state. The default disconnected circuit retention is only 3 minutes. Unlike WebForms where ViewState survives a page refresh (it is in the HTML), Blazor Server has no built-in state persistence across circuit loss. This is the single biggest behavior difference users will notice coming from WebForms.

**How to avoid:**
- Implement auto-save for all forms with significant user input. Persist drafts to server storage (database or distributed cache) on every meaningful change.
- Extend `DisconnectedCircuitRetentionPeriod` to a value appropriate for the use case (e.g., 10-30 minutes), understanding memory implications.
- Customize the reconnection UI to show clear user feedback and attempt automatic page reload after reconnection failure.
- For critical workflows, use `PersistentComponentState` to survive prerendering, and implement your own persistence for circuit loss.
- Design components to be "rehydratable" -- on initialization, check for saved draft state and restore it.
- Configure sticky sessions in the load balancer (required for Blazor Server with multiple backend instances).

**Warning signs:**
- Users report losing form data after brief inactivity.
- "Attempting to reconnect" messages appear frequently in testing.
- Memory usage on the server grows proportionally with concurrent users and does not decrease.
- Mobile users have consistently worse experience than desktop users.

**Phase to address:**
Phase 2 (First Page Migration) -- build the auto-save/rehydration pattern into the very first migrated page so it becomes a template for all subsequent pages.

**Confidence:** HIGH (documented at [Microsoft Learn](https://learn.microsoft.com/aspnet/core/blazor/host-and-deploy/server/memory-management?view=aspnetcore-10.0), [SignalR guidance](https://learn.microsoft.com/en-us/aspnet/core/blazor/fundamentals/signalr?view=aspnetcore-9.0), [GitHub Issue #48724](https://github.com/dotnet/aspnetcore/issues/48724))

---

## Technical Debt Patterns

Shortcuts that seem reasonable but create long-term problems.

| Shortcut | Immediate Benefit | Long-term Cost | When Acceptable |
|----------|-------------------|----------------|-----------------|
| Copy-paste WebForms code-behind into Blazor component `@code` blocks | Fast page-by-page migration | God components with 1000+ lines, untestable business logic mixed with UI, impossible to reuse | Never. Extract business logic to services from day one. |
| Using `HttpContext` directly in Blazor components via System.Web Adapters | Familiar WebForms patterns work | Breaks when rendering mode changes, null during SignalR interactions, couples components to HTTP pipeline | Only during SSR rendering for initial data load. Never for interactive components. |
| Registering `DbContext` as Scoped in Blazor Server | Standard ASP.NET Core pattern | Circuit-lifetime scope means DbContext lives too long, tracks too many entities, stale data, memory pressure | Never in Blazor Server. Use `IDbContextFactory<T>` and create/dispose per operation. |
| Skipping dead code analysis and porting everything | "Complete" migration, no risk of missing something | 30-60% wasted effort on unused features, larger codebase to maintain, more bugs to fix | Never. Always audit first. |
| Wrapping EF Core around every legacy stored procedure | Uniform data access layer | Stored procedures with side effects, complex logic, or temp tables do not map well to EF Core. Performance degrades. | Never for complex SPs. Use Dapper or raw ADO.NET for these. |
| Using `Thread.Sleep` or `Task.Wait()` ported from WebForms synchronous code | Code compiles and "works" | Exhausts ASP.NET Core thread pool, causes deadlocks, kills Blazor Server scalability | Never. All I/O must be async. |
| Implementing Clean Architecture with full CQRS/MediatR from day one during migration | "Proper" architecture | Over-engineering delays migration, adds complexity during an already complex transition, team must learn two things at once | Only after migration is complete. During migration, use simple service layer. |

---

## Integration Gotchas

Common mistakes when connecting the old and new systems during incremental migration.

| Integration | Common Mistake | Correct Approach |
|-------------|----------------|------------------|
| YARP -> WebForms proxy | Not forwarding `X-Forwarded-Host`, `X-Forwarded-Proto`, `X-Forwarded-PathBase` headers, causing the legacy app to generate wrong URLs | Enable `AddProxySupport()` on the WebForms side via System.Web Adapters. Configure `UseForwardedHeaders` on ASP.NET Core side with explicit `KnownProxies`. |
| Session sharing | Registering session keys in the adapter config but using different key names or serialization formats between old/new | Maintain a single source-of-truth file listing all shared session keys with their types. Use JSON serialization consistently. Test round-trip serialization. |
| Authentication sharing | Assuming FormsAuth cookie can be read directly by ASP.NET Core cookie auth | Use System.Web Adapters remote authentication. The Core app calls the Framework app to validate the auth state. Do not try to share/decrypt cookies directly. |
| Shared database access | Both apps hitting the same tables concurrently with different ORM configurations (e.g., different isolation levels, different locking behavior) | Use identical transaction isolation levels. Be aware that EF Core uses optimistic concurrency by default while legacy code may use pessimistic locking. Add concurrency tokens (`rowversion`) to critical tables. |
| Static assets (CSS/JS) | Serving static assets from both apps, causing style conflicts or duplicate jQuery/Bootstrap versions | Consolidate static assets to the ASP.NET Core app. Use YARP transforms to redirect static asset requests. Ensure only one version of shared libraries loads. |
| WebForms UpdatePanel AJAX | YARP proxying partial postback responses incorrectly due to content-type or encoding issues | Ensure YARP does not modify `Content-Type` headers for `__doPostBack` requests. Disable response buffering for proxied WebForms AJAX calls. |

---

## Performance Traps

Patterns that work at small scale but fail under production load.

| Trap | Symptoms | Prevention | When It Breaks |
|------|----------|------------|----------------|
| Blazor Server with no circuit limits | Server memory grows unbounded, eventually OOM | Set `MaxRetainedDisconnectedCircuits` and `DisconnectedCircuitMaxRetained`. Budget ~250KB per circuit minimum, more for complex pages. Monitor with `dotnet-counters`. | At ~500-2000 concurrent users per server (depends on component complexity) |
| EF Core N+1 queries on legacy schema | Page load times grow linearly with data. SQL Profiler shows hundreds of queries per page render. | Use `.Include()` / `.ThenInclude()` for eager loading. Audit every page's query count during development. Use `AsSplitQuery()` for queries with multiple includes. | At ~100 rows in related collections |
| Scoped DbContext in Blazor Server accumulating tracked entities | Memory per circuit grows over time. Queries slow down as change tracker scans more entities. | Use `IDbContextFactory<T>`. Create and dispose per operation. Call `AsNoTracking()` for read-only queries. | After ~50 operations per circuit (user session) |
| Synchronous legacy code running on ASP.NET Core thread pool | Server becomes unresponsive under moderate load. Thread pool starvation. Other requests time out. | Convert all I/O operations to async. Use `Task.Run()` temporarily for truly synchronous legacy code that cannot be converted yet, but track and eliminate. | At ~100 concurrent requests with blocking I/O |
| YARP double-hop latency | Every proxied request adds network roundtrip. Legacy app behind YARP is measurably slower. | Deploy YARP and legacy app on same machine or very low-latency network. Use connection pooling in YARP. Monitor proxy latency via middleware. | Noticeable when proxy adds >5ms per request and page makes multiple proxied calls |
| Blazor Server rendering large collections without virtualization | Browser becomes unresponsive. SignalR message size explodes. | Use `<Virtualize>` component for any list >50 items. Implement server-side paging. | At ~200+ items rendered in a single component |

---

## Security Mistakes

Migration-specific security issues beyond general web security.

| Mistake | Risk | Prevention |
|---------|------|------------|
| Exposing legacy app directly to the internet alongside YARP proxy | Bypass all new security middleware, CORS, rate limiting | Legacy app should ONLY accept connections from YARP (bind to localhost or use firewall rules). Validate `X-Forwarded-For` header on legacy side. |
| System.Web Adapters API key in source control | Anyone can call the remote session/auth endpoints on the legacy app | Store adapter API keys in Azure Key Vault or environment variables. Rotate on schedule. Use HTTPS between YARP and legacy app even on localhost. |
| Not re-validating authorization on Blazor components | Legacy page-level authorization checks don't transfer to Blazor components automatically | Apply `[Authorize]` attributes to every Blazor page/component. Re-implement role/policy checks. Do not assume auth "just works" because it worked in WebForms. |
| FormsAuthentication ticket lifetime mismatch | Users authenticated on legacy side appear unauthenticated on Blazor side (or vice versa) because ticket expiration differs | Align authentication timeout values between both applications. Test edge cases around ticket renewal. |
| Tenant context spoofing via YARP headers | Malicious requests inject tenant identifiers via headers, accessing other tenants' data | Validate tenant context server-side from authenticated user claims, never from request headers alone. Apply defense-in-depth with EF Core query filters. |

---

## UX Pitfalls

User experience mistakes specific to the incremental migration approach.

| Pitfall | User Impact | Better Approach |
|---------|-------------|-----------------|
| Inconsistent navigation between legacy and new pages | Users experience jarring full-page reloads when crossing the proxy boundary, different styling, different loading behavior | Use consistent master layout/CSS framework across both apps. Minimize visible boundary crossing. Pre-load Blazor framework on legacy pages. |
| "Attempting to reconnect" toast with no recovery guidance | Users stare at a modal overlay, not knowing if their data is saved | Customize reconnect UI to show: "Your data is saved. Reconnecting..." if auto-save is enabled. After timeout, auto-reload the page. |
| WebForms GridView replaced with Blazor table lacking sorting/filtering/paging | Users had rich grid functionality in WebForms; new pages feel like a downgrade | Use a mature component library (MudBlazor DataGrid, Telerik Grid, etc.) from the start. Match or exceed the legacy grid's capabilities. |
| Different validation behavior between legacy and new pages | Users learn one set of rules on old pages, encounter different behavior on new pages | Document legacy validation rules before migration. Replicate exactly in Blazor, including error message text and positioning. |
| Loading spinners where WebForms had instant (postback) responses | Blazor's async rendering shows loading states that WebForms' synchronous postbacks did not | Implement skeleton screens instead of spinners. Use streaming rendering where possible. Optimize first meaningful paint time. |

---

## "Looks Done But Isn't" Checklist

Things that appear complete in demo/dev but fail in production or at scale.

- [ ] **YARP Proxy:** Works in dev with both apps on localhost -- verify it works with production networking (different machines, load balancer, HTTPS termination, correct `X-Forwarded-*` headers).
- [ ] **Session Sharing:** Works for a few hardcoded keys -- verify ALL session keys used in production are registered, serialized correctly, and survive round-trip between apps.
- [ ] **Authentication:** Works when you test login/navigate sequentially -- verify it works when browser has stale cookies, when user is idle for extended periods, and when legacy auth ticket expires mid-circuit.
- [ ] **Multi-Tenancy:** Works for single tenant in development -- verify cross-tenant data isolation with explicit negative tests. Verify tenant resolution works through YARP proxy boundary.
- [ ] **EF Core Queries:** Return correct data in dev -- verify query performance on production-sized data. Check for N+1 patterns, missing indexes for EF Core's query patterns (which may differ from legacy ORM).
- [ ] **Blazor Forms:** Work in happy path -- verify behavior on circuit loss, browser back button, multiple tab open, concurrent edit scenarios.
- [ ] **Error Handling:** Blazor shows error UI -- verify unhandled exceptions in circuits do not leak to other users, and that circuit-level errors produce useful diagnostics (not generic 500s).
- [ ] **Mobile Responsiveness:** Pages render in dev on desktop -- verify SignalR reconnection on mobile networks, sleep/wake behavior, and touch interaction with Blazor components.
- [ ] **Zero Downtime Deploy:** Individual apps deploy without downtime -- verify that deploying the ASP.NET Core app does not break in-flight requests proxied to the legacy app, and that YARP configuration changes apply without restart.
- [ ] **Dead Code Audit:** Migration scope agreed -- verify with production analytics that no actively-used feature was excluded. Run analytics for a minimum of 4 weeks covering month-end and other periodic workflows.

---

## Recovery Strategies

When pitfalls occur despite prevention, how to recover.

| Pitfall | Recovery Cost | Recovery Steps |
|---------|---------------|----------------|
| YARP routing collision | LOW | Add explicit route priority numbers. Re-test all deep links. Can be fixed in config without code changes. |
| Session state null in Blazor | MEDIUM | Refactor affected components to load state during SSR and cache in circuit. May require re-architecting state flow for affected pages. |
| Dead code ported forward | HIGH | Conduct retroactive usage audit. Remove unused migrated features. Wasted effort is sunk cost but stop further investment. |
| Multi-tenancy data leak | CRITICAL | Immediate incident response. Audit all queries for tenant filter. Add global query filter tests. May require data audit and customer notification. |
| Auth redirect loops | LOW-MEDIUM | Fix forwarded headers config and cookie paths. Can usually be resolved in a few hours once diagnosed. Use browser DevTools network tab to trace redirect chain. |
| EF Core scaffolding failures | MEDIUM | Switch to EF Core Power Tools. Use partial classes. Accept Dapper for problem areas. Incremental fix -- does not require rework of already-working entities. |
| Circuit state loss causing data loss | MEDIUM | Implement auto-save retroactively. Requires touching every form component. Build as a shared base component or service to minimize per-page effort. |
| Thread pool starvation from sync code | MEDIUM | Profile to identify blocking calls. Wrap in `Task.Run()` as immediate fix. Schedule async conversion as follow-up work. |

---

## Pitfall-to-Phase Mapping

How roadmap phases should address these pitfalls.

| Pitfall | Prevention Phase | Verification |
|---------|------------------|--------------|
| YARP routing collision | Phase 0 (Infrastructure) | Integration test: every Blazor `@page` route resolves to Blazor, every WebForms route proxies correctly |
| Session state null in Blazor | Phase 0 (Infrastructure) + Phase 1 (First Migration) | End-to-end test: set session value in WebForms, read in Blazor SSR, verify in Blazor interactive |
| Dead code ported forward | Phase 0 (Discovery/Audit) | Usage analytics report reviewed and signed off by business stakeholder before migration scope is finalized |
| Multi-tenancy data leak | Phase 0 (Infrastructure) + Every phase | Cross-tenant integration tests in CI. Automated scan that every DbContext entity has TenantId filter. |
| Auth redirect loops | Phase 0 (Infrastructure) | Selenium/Playwright test: login on legacy, navigate to Blazor page, verify authenticated. Reverse flow. Test with expired cookie. |
| EF Core scaffolding failures | Phase 1 (Data Access Layer) | Scaffolding produces compilable code. Sample queries return correct data validated against direct SQL results. |
| Circuit state loss | Phase 1 (First Migration) | Playwright test: fill form, kill SignalR connection, reconnect, verify data persisted. Test on mobile device. |
| Thread pool starvation | Phase 1+ (Every Migration) | Load test with realistic concurrent users. Monitor thread pool queue length via `dotnet-counters`. |
| Over-engineered architecture | Phase 0 (Architecture Decision) | Architecture decision record (ADR) that explicitly chooses simple service layer for migration phase. |
| Cookie/header misconfiguration | Phase 0 (Infrastructure) | Test with production-like networking (separate hosts, HTTPS, load balancer). Not just localhost. |

---

## Phase-Specific Warnings

| Phase Topic | Likely Pitfall | Mitigation |
|-------------|---------------|------------|
| Infrastructure setup (YARP + Adapters) | Routing collision, auth redirect loops, header forwarding | Build comprehensive integration test suite before migrating any page. This phase has the most "invisible" failures. |
| First page migration | Underestimating Blazor lifecycle differences from WebForms | Choose a simple, low-risk page for first migration. Not the most important page -- the most isolated one. |
| Data access layer (EF Core) | Scaffolding failures, N+1 queries, concurrency conflicts with legacy ORM | Start with read-only scenarios. Add write operations only after read paths are validated. |
| Bulk page migration | Fatigue leads to copy-paste patterns, components become monolithic | Enforce code review checklist. Extract common patterns to shared components/services early. |
| Session/state elimination | Removing session dependency breaks features that secretly depended on it | Map all session key usage before removing any. Use feature flags to switch between session-based and state-based implementations. |
| Legacy app decommission | "One more thing" that was missed in migration scope | Run both apps in parallel for at least 2 weeks with production traffic after "complete" migration. Monitor legacy app access logs for unexpected requests. |
| Testing strategy | E2E tests are flaky due to Blazor's async rendering | Use Playwright with proper wait strategies (`WaitForSelector`, network idle). Do not use fixed `Thread.Sleep` delays. Build page object model for both legacy and new apps to compare behavior. |

---

## Sources

### Official Microsoft Documentation
- [Migrate from ASP.NET Web Forms to Blazor](https://learn.microsoft.com/en-us/dotnet/architecture/blazor-for-web-forms-developers/migration) -- HIGH confidence
- [State Management in Blazor for Web Forms Developers](https://learn.microsoft.com/dotnet/architecture/blazor-for-web-forms-developers/state-management) -- HIGH confidence
- [Incremental ASP.NET to ASP.NET Core Migration](https://devblogs.microsoft.com/dotnet/incremental-asp-net-to-asp-net-core-migration/) -- HIGH confidence
- [Enable Blazor Server with YARP in Incremental Migration](https://learn.microsoft.com/aspnet/core/migration/fx-to-core/inc/blazor?view=aspnetcore-7.0) -- HIGH confidence
- [Manage Memory in Deployed Blazor Server Apps](https://learn.microsoft.com/aspnet/core/blazor/host-and-deploy/server/memory-management?view=aspnetcore-10.0) -- HIGH confidence
- [Blazor DI Scope and Circuit Lifetime](https://learn.microsoft.com/aspnet/core/blazor/fundamentals/dependency-injection?view=aspnetcore-10.0) -- HIGH confidence
- [EF Core Reverse Engineering / Scaffolding](https://learn.microsoft.com/en-us/ef/core/managing-schemas/scaffolding/) -- HIGH confidence
- [EF Core Multi-tenancy](https://learn.microsoft.com/en-us/ef/core/miscellaneous/multitenancy) -- HIGH confidence
- [YARP Header Guidelines](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/servers/yarp/header-guidelines?view=aspnetcore-9.0) -- HIGH confidence
- [YARP Session Affinity](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/servers/yarp/session-affinity?view=aspnetcore-10.0) -- HIGH confidence
- [ASP.NET Framework to Core Authentication Migration](https://learn.microsoft.com/en-us/aspnet/core/migration/fx-to-core/areas/authentication?view=aspnetcore-10.0) -- HIGH confidence
- [System.Web Adapters v1.2](https://devblogs.microsoft.com/dotnet/systemweb-adapters-1_2/) -- HIGH confidence

### GitHub Issues (Real-World Problems)
- [Session Sharing Between WebForms and Blazor Server - Issue #413](https://github.com/dotnet/systemweb-adapters/issues/413) -- HIGH confidence
- [Session Sharing Not Working - Issue #453](https://github.com/dotnet/systemweb-adapters/issues/453) -- HIGH confidence
- [Intermittent Remote Session Errors - Issue #556](https://github.com/dotnet/systemweb-adapters/issues/556) -- HIGH confidence
- [Auth Redirect Failing - Issue #495](https://github.com/dotnet/systemweb-adapters/issues/495) -- HIGH confidence
- [Remote Auth Not Working in Azure - Issue #478](https://github.com/dotnet/systemweb-adapters/issues/478) -- HIGH confidence
- [YARP URL Rewriting Conflict - Issue #2532](https://github.com/dotnet/yarp/issues/2532) -- HIGH confidence
- [YARP Cookie Domain Mismatch - Issue #2100](https://github.com/microsoft/reverse-proxy/issues/2100) -- HIGH confidence
- [Blazor Server Circuit State Persistence - Issue #60494](https://github.com/dotnet/aspnetcore/issues/60494) -- HIGH confidence
- [Blazor Server Reconnection Issues - Issue #48724](https://github.com/dotnet/aspnetcore/issues/48724) -- HIGH confidence

### Real-World Migration Case Studies
- [Jimmy Bogard: Tales from the .NET Migration Trenches (full series)](https://www.jimmybogard.com/tales-from-the-net-migration-trenches/) -- HIGH confidence
- [Jimmy Bogard: Session State Migration](https://www.jimmybogard.com/tales-from-the-net-migration-trenches-session-state/) -- HIGH confidence
- [Trailhead Technology: Upgrading WebForms to Blazor with YARP](https://trailheadtechnology.com/upgrading-an-asp-net-web-forms-app-to-blazor-incrementally-with-yarp/) -- MEDIUM confidence
- [ErikEJ: EF Core SQL Server Scaffolding Gotchas](https://erikej.github.io/efcore/2020/10/12/ef-core-sqlserver-scaffolding-gotchas.html) -- HIGH confidence

### Community & Industry Guidance
- [Devox Software: Modernizing ASP.NET WebForms 2025 Engineering Playbook](https://devoxsoftware.com/blog/modernizing-asp-net-webforms-a-2025-engineering-playbook/) -- MEDIUM confidence
- [Jeremy Likness: Multi-tenancy with EF Core in Blazor Server](https://blog.jeremylikness.com/blog/multitenancy-with-ef-core-in-blazor-server-apps/) -- MEDIUM confidence
- [Blazor Server Memory Management: Stop Circuit Leaks](https://amarozka.dev/blazor-server-memory-management-circuit-leaks/) -- MEDIUM confidence
- [DI Scopes in Blazor (Thinktecture)](https://www.thinktecture.com/en/blazor/dependency-injection-scopes-in-blazor/) -- MEDIUM confidence

---
*Pitfalls research for: ASP.NET WebForms to .NET 10 + Blazor Modernization*
*Researched: 2026-02-13*
