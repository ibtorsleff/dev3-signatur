# Project State

## Project Reference

See: .planning/PROJECT.md (updated 2026-02-13)

**Core value:** The activity list view must work perfectly. Users must be able to see and navigate their recruitment activities with correct status.
**Current focus:** Phase 03.9 COMPLETE -- Redo ActivityCreateEdit UI Migration with Full Fidelity

## Current Position

Phase: 3.9 of 8 (Redo ActivityCreateEdit UI Migration with Full Fidelity)
Plan: 2 of 2 in current phase
Status: Plan 03.9-02 COMPLETE -- Phase 03.9 COMPLETE
Last activity: 2026-02-23 -- Plan 03.9-02 complete

Progress: [##########] 100%

## Performance Metrics

**Velocity:**
- Total plans completed: 28
- Average duration: 5 minutes
- Total execution time: 1.91 hours

**By Phase:**

| Phase | Plans | Total | Avg/Plan |
|-------|-------|-------|----------|
| 01-infrastructure-shell | 4 | 28 min | 7 min |
| 03-core-read-views | 6 | 30 min | 5 min |
| 03.1-route-aware-nav-activity-modes | 2 | 5 min | 2.5 min |
| 03.2-activity-list-layout-matching | 1 | 6 min | 6 min |
| 03.3-activity-list-conditional-columns | 2 | 11 min | 5.5 min |
| 03.4-activity-list-row-height-pagination-styling | 1 | 2 min | 2 min |
| 03.5-localization | 5 | 15 min | 3 min |
| 03.6-permission-migration | 2 | 5 min | 2.5 min |

| 03.7.1-icurrentuserservice-db-backed-user-context | 2 | 3 min | 1.5 min |
| 03.8-client-selector-dropdown | 2 | 5 min | 2.5 min |

**Recent Trend:**
- Last 5 plans: 03.7-01 (n/a), 03.7.1-01 (1 min), 03.7.1-02 (2 min), 03.8-01 (3 min), 03.8-02 (2 min)
- Trend: UI toolbar controls with role/permission gating in 2 min

*Updated after each plan completion*

| Phase Plan | Duration | Tasks | Files |
|------------|----------|-------|-------|
| 01-01 | 4 min | 2 | 32 |
| 01-02 | 6 min | 2 | 9 |
| 01-03 | 9 min | 2 | 18 |
| 01-04 | 9 min | 2 | 5 |
| 03-01 | 9 min | 2 | 13 |
| 03-02 | 3 min | 2 | 14 |
| 03-03 | 5 min | 2 | 10 |
| 03-04 | 5 min | 2 | 13 |
| 03-05 | 4 min | 2 | 6 |
| 03-06 | 4 min | 2 | 6 |
| 03.1-01 | 2 min | 2 | 5 |
| 03.1-02 | 3 min | 2 | 4 |
| 03.2-01 | 6 min | 3 | 10 |
| 03.3-01 | 3 min | 2 | 6 |
| 03.3-02 | 8 min | 2 | 7 |
| 03.4-01 | 2 min | 2 | 3 |
| 03.5-01 | 8 min | 3 | 8 |
| 03.5-02 | 2 min | 1 | 2 |
| 03.5-03 | 2 min | 2 | 4 |
| 03.5-04 | 2 min | 2 | 4 |
| 03.5-05 | 1 min | 1 | 1 |
| 03.6-01 | 3 min | 2 | 8 |
| 03.6-02 | 2 min | 2 | 5 |
| 03.7.1-01 | 1 min | 2 | 3 |
| 03.7.1-02 | 2 min | 2 | 2 |
| 03.8-01 | 3 min | 2 | 6 |
| 03.8-02 | 2 min | 1 | 2 |
| 03.9-01 | 8 min | 2 | 2 |
| 03.9-02 | 3 min | 1 | 1 |

## Accumulated Context

### Decisions

Decisions are logged in PROJECT.md Key Decisions table.
Recent decisions affecting current work:

- [Roadmap]: 6 phases derived from 61 requirements -- Infrastructure first, then multi-tenancy, read views, write ops, localization/UX, testing/deployment
- [Roadmap]: Multi-tenancy and security split into its own phase (Phase 2) because it is a hard dependency for all data access in Phases 3+
- [Roadmap]: Testing/deployment/monitoring grouped into final phase rather than spread across phases -- allows focused hardening after features complete
- [01-01]: Used .NET 10 SDK with .slnx solution format (new XML-based format in .NET 10)
- [01-01]: Created empty Blazor app with --empty flag for minimal starting point
- [01-02]: Session keys registered based on legacy codebase audit: UserId, SiteId, ClientId, UserName, UserLanguageId (5 core keys)
- [01-02]: Navigation color scheme #1a237e (dark blue) chosen as professional baseline
- [01-02]: Legacy .aspx routes left as direct links - YARP handles proxying automatically
- [01-03]: Database-first EF Core scaffolding used - no migrations created as legacy schema is immutable
- [01-03]: Scaffolded 9 core tables for Phase 1-3 requirements (Client, ERActivity, ERCandidate, aspnet auth, Permission, UserActivityLog, Site)
- [01-04]: TUnit selected for unit tests with Microsoft.Testing.Platform (new .NET 10 pattern)
- [01-04]: Playwright with NUnit runner for E2E tests - all 3 browser engines installed
- [03-01]: MudBlazor 8.0.0 installed for UI components (resolved from 7.23.0)
- [03-01]: Custom teal theme configured (#1a9b89 primary, #178a79 darken)
- [03-01]: ERActivityMember, BinaryFile, ERCandidateFile entities scaffolded from database
- [03-01]: ERActivityStatus enum created with values matching legacy system (All=0, OnGoing=1, Closed=2, Deleted=3, Draft=4)
- [03-01]: StatusMappings static dictionary helper for status name lookups
- [03-01]: Global query filter for Eractivitymember scopes through activity.ClientId
- [03-02]: Upgraded System.Linq.Dynamic.Core to 1.7.1 (resolved GHSA-4cv2-4hjh-77rx security vulnerability)
- [03-02]: StatusName computed in-memory after EF materialization (StatusMappings cannot be translated to SQL)
- [03-02]: Code-behind pattern (.razor.cs) used for component logic separation
- [03-02]: Permission-based filtering applied to activity list (non-admin users see only their activities)
- [03-02]: GridRequest/GridResponse DTO pattern established for all server-side grids
- [03-03]: User table scaffolded with no tenant filter (accessed only through ERActivityMember joins, already tenant-filtered)
- [03-03]: AsSplitQuery used for activity detail to avoid cartesian explosion between members and candidates
- [03-03]: StatusMappings and member type names resolved in-memory after EF query materialization
- [03-04]: Candidate status mapping hardcoded for Phase 3 (TODO Phase 5: database-driven localized lookup)
- [03-04]: File download via DotNetStreamReference and JS interop (creates blob URL and triggers browser save)
- [03-04]: SignalR MaximumReceiveMessageSize set to 10MB for file downloads
- [03-04]: File ownership verified before download (joins through candidate-activity-tenant chain)
- [03-05]: IUserSessionContext changed to nullable int? for UserId/SiteId/ClientId (session may not be available outside SSR)
- [03-06]: Error handling pattern: detail pages use _errorMessage + MudAlert, list pages use ISnackbar + empty GridData fallback
- [03-06]: No ILogger added yet -- Phase 5 will add structured logging infrastructure
- [03.1-01]: NavigationConfigService registered as Singleton (stateless pure function of path)
- [03.1-01]: Row 2 mode tabs shown unconditionally (permission-based visibility deferred to later phase)
- [03.1-01]: Detail page /activities/123 maps to Ongoing tab (detail is within ongoing context)
- [03.1-01]: Service registered in Program.cs since it lives in Web project namespace
- [03.1-02]: Status filter applied server-side in SQL WHERE clause before count and pagination
- [03.1-02]: OnParametersSet used for mode detection (Blazor reuses component during SPA navigation)
- [03.1-02]: Default null statusFilter parameter ensures backward compatibility with existing callers
- [03.1-02]: No route conflict: /activities/{Mode} (string) vs /activities/{ActivityId:int} (constrained)
- [03.2-01]: CSS custom properties for portal theming — zero hardcoded colors in grid base
- [03.2-01]: theme-recruitingportal / theme-adportal naming convention
- [03.2-01]: 7px solid border instead of padding for visible grid frame
- [03.2-01]: inline-flex on .column-header keeps sort icons next to label text
- [03.2-01]: Sort icons always visible at 0.4 opacity
- [03.2-01]: Hover color lighter than header for visual distinction
- [03.2-01]: Dense mode removed, explicit CSS padding for exact legacy match
- [03.3-01]: User name resolution via correlated SQL subqueries (Guid FKs without navigation properties)
- [03.3-01]: ClientSection and ErTemplateGroup as minimal Id+Name entities (lookup tables)
- [03.3-01]: No global query filters on lookup tables (shared reference data)
- [03.3-01]: JournalNo and StatusName removed from ActivityListDto (legacy list does not display these)
- [03.3-02]: HeaderStyle per column instead of ColGroup (ColGroup leaves whitespace when Hidden columns toggled off)
- [03.3-02]: Headline as PropertyColumn not TemplateColumn (consistent header styling)
- [03.3-02]: WebAdVisitor entity for click count column (client config check hardcoded as TODO)
- [03.3-02]: Copy action icon served from legacy via YARP (same pattern as nav icons)
- [03.4-01]: 1px vertical padding + 17px line-height = 19px row height (math: 1+17+1=19)
- [03.4-01]: Height on both tr and td for cross-browser compatibility
- [03.4-01]: overflow:hidden on cells to prevent content overflow at reduced height
- [03.4-01]: Pagination toolbar border-top uses theme cell border color (replaces MudBlazor default)
- [03.5-01]: Cache key pattern loc_{languageId}_{key} shared between LocalizationService and warmup service
- [03.5-01]: IMemoryCache for localization (not IDistributedCache) -- translations are read-only reference data
- [03.5-01]: Localization entity composite PK (Key, LanguageId, SiteId) -- Id is identity but not PK
- [03.5-01]: Language fallback chain: requested -> English (1) -> [key] bracket notation
- [Phase 03.5-02]: PascalCase English identifier keys as localization placeholders -- exact legacy DB keys matched during full migration
- [Phase 03.5-02]: L alias for ILocalizationService via [Inject] property for concise Razor template syntax
- [Phase 03.5-03]: LabelKey as optional nullable property on NavMenuItem for localization key mapping
- [Phase 03.5-03]: Singleton-returns-keys/scoped-resolves-text pattern for DI lifetime mismatch between NavigationConfigService and ILocalizationService
- [Phase 03.5-03]: Bracket-notation fallback check (StartsWith '[') to detect unresolved keys -- SUPERSEDED by 03.5-05
- [Phase 03.5-05]: Removed bracket-notation suppression -- GetText return value is always the correct display value when a key is configured
- [Phase 03.5-04]: Singleton + hosted service pattern (AddSingleton + AddHostedService factory) for injectable IHostedService
- [Phase 03.5-04]: Admin page pattern at /admin/* with [Authorize] and MudBlazor layout
- [Phase 03.6-01]: IsClientUser computed as ClientId.HasValue && ClientId.Value > 0 -- matches legacy PermissionHelper.UserIsClient
- [Phase 03.6-01]: PortalPermission enum includes all 90+ values upfront matching legacy DB PermissionId exactly
- [Phase 03.6-01]: ERecruitmentPermission fully deleted (clean break, not deprecated)
- [Phase 03.6-02]: IPermissionHelper registered in DependencyInjection.cs (Infrastructure layer) rather than Program.cs to follow established DI registration pattern
- [Phase 03.7.1-01]: CurrentUserDto record used instead of returning User entity directly (Application does not reference Infrastructure)
- [Phase 03.7.1-01]: _loaded bool flag pattern (not null-check) to handle user-not-found without infinite retry
- [Phase 03.7.1-02]: ICurrentUserService registered after IPermissionHelper in DI (user-context services grouped together)
- [Phase 03.7.1-02]: GetUserGuidAsync deleted entirely rather than deprecated (clean break, no aspnet_Users reference remains in ActivityService)
- [Phase 03.8-01]: SqlQueryRaw with ObjectData.value() XPath for client name extraction -- no C# XML parsing, matches legacy SQL pattern
- [Phase 03.8-01]: clientIdFilter as optional parameter with default null -- backward compatible, no breaking changes to existing callers
- [Phase 03.8-01]: ClientService bypasses EF global filters via raw SQL -- does not set CurrentSiteId/CurrentClientId on context
- [Phase 03.8-02]: MudSelect with -1 sentinel for "All Clients" -- simpler than nullable int with MudBlazor select
- [Phase 03.8-02]: Draft mode navigates to ActivityCreateDraft.aspx, other modes to ActivityCreateEdit.aspx -- matches legacy routing pattern
- [Phase 03.9-01]: ValidationContext.GetService is non-generic in System.ComponentModel.DataAnnotations -- use GetService(typeof(T)) as T pattern
- [Phase 03.9-01]: Edit save navigation goes to /recruiting/activities (not Snackbar + stay on page) -- matches legacy behavior
- [Phase 03.9-01]: _saving = false only in catch block for OnSaveAsync -- navigation destroys component, reset in finally is unnecessary
- [Phase 03.9-01]: RecruitmentTypeId ternary derivation in MapEditDataToForm: LeadershipPositionId==1 -> type 2, BlindRecruitmentId==1 -> type 3, else type 1 or null
- [Phase 03.9-01]: CalendarTypeId derived from checkbox state in BuildSaveCommand: ClosedCalendar=2, OpenCalendar=1, both=use dropdown, neither=0
- [Phase 03.9-01]: _cascadeLoading field added to ActivityCreateEdit -- toggled true/false around LoadClientDependentDataAsync in OnClientChangedAsync
- [Phase 03.9-02]: LeadershipPosition and BlindRecruitment not shown as standalone UI dropdowns -- only RecruitmentTypeId dropdown shown; bidirectional translation to binary DB flags handled in code-behind
- [Phase 03.9-02]: Client autocomplete excluded from cascade disabled state -- it is the trigger for cascade reload, not a dependent field

### Pending Todos

1. ClientSection (Afdeling) autocomplete hierarchical display — `.planning/todos/pending/2026-02-23-clientsection-afdeling-autocomplete-hierarchical-display.md`

### Roadmap Evolution

- Phase 3.2 inserted after Phase 3: Activity List Layout Matching (URGENT) — research legacy activity list layout and replicate in Blazor MudDataGrid, hide filter options behind toggle
- Phase 3.3 inserted after Phase 3.2: Activity List Conditional Columns (URGENT) — research and fix column visibility per mode/role/permission to match legacy for all 3 modes
- Phase 3.4 inserted after Phase 3.3: Activity List Row Height & Pagination Styling (URGENT) — reduce row height to 19px, style pagination footer to match grid header
- Phase 3.5 inserted after Phase 3: make the full legacy localization/globalization system available in the blazor app. example is the GetText, which is available via the basepage.cs in the legacy app. the database cannot be altered (URGENT)
- Phase 03.6 inserted after Phase 3: user/client permission helper migration - isClientLoggedOn and role/permission checks (URGENT)
- Phase 03.7 inserted after Phase 3: Authentication & User Context — Migration Strategy (URGENT)
- Phase 03.7.1 inserted after Phase 03.7: ICurrentUserService — DB-Backed User Context (URGENT)
- Phase 03.8 inserted after Phase 3: Client selector dropdown and Create Activity button for non-client users on ActivityList (URGENT)
- Phase 03.9 inserted after Phase 3: Redo ActivityCreateEdit UI migration with full fidelity (URGENT)

### Blockers/Concerns

- [Research]: MediatR licensing eligibility needs verification before Phase 1 -- consider Wolverine as alternative if ineligible
- [Research]: Exact session keys used by legacy WebForms app need auditing during Phase 1
- [Research]: Phase 5 localization may need research-phase if GetText + DB pattern proves non-standard

## Session Continuity

Last session: 2026-02-23
Stopped at: Completed 03.9-02-PLAN.md (ActivityCreateEdit razor markup defect fixes)
Resume file: None

**Phase 03.9 COMPLETE**: All markup defects fixed — correct localization keys, LeadershipPosition/BlindRecruitment dropdowns removed, cascade disabled states applied to all client-dependent template inputs.
