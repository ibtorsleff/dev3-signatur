# Project State

## Project Reference

See: .planning/PROJECT.md (updated 2026-02-13)

**Core value:** The activity list view must work perfectly. Users must be able to see and navigate their recruitment activities with correct status.
**Current focus:** Phase 3: Core Read Views

## Current Position

Phase: 3 of 6 (Core Read Views)
Plan: 6 of 6 in current phase
Status: Complete
Last activity: 2026-02-15 -- Completed plan 03-06: Error Handling for all activity/candidate pages (try-catch in lifecycle methods, circuit resilience)

Progress: [######....] 58%

## Performance Metrics

**Velocity:**
- Total plans completed: 10
- Average duration: 5 minutes
- Total execution time: 0.93 hours

**By Phase:**

| Phase | Plans | Total | Avg/Plan |
|-------|-------|-------|----------|
| 01-infrastructure-shell | 4 | 28 min | 7 min |
| 03-core-read-views | 6 | 30 min | 5 min |

**Recent Trend:**
- Last 5 plans: 03-02 (3 min), 03-04 (5 min), 03-03 (5 min), 03-05 (4 min), 03-06 (4 min)
- Trend: Consistent Phase 3 velocity around 4-5 min per plan

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

### Pending Todos

None yet.

### Blockers/Concerns

- [Research]: MediatR licensing eligibility needs verification before Phase 1 -- consider Wolverine as alternative if ineligible
- [Research]: Exact session keys used by legacy WebForms app need auditing during Phase 1
- [Research]: Phase 5 localization may need research-phase if GetText + DB pattern proves non-standard

## Session Continuity

Last session: 2026-02-15
Stopped at: Re-executed 03-05-PLAN.md with actual code changes -- nullable int? applied to IUserSessionContext and all dependents
Resume file: None

**Phase 3 Progress**: 6 of 6 plans complete (including 2 gap-closure plans for session nullability and error handling). All read views implemented with defensive error handling. Phase 3 complete - ready to move to Phase 4 (Write Operations).
