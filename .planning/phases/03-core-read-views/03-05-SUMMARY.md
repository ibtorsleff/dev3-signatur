---
phase: 03-core-read-views
plan: 05
subsystem: database
tags: [multi-tenancy, nullable, session, query-filter, ef-core]

# Dependency graph
requires:
  - phase: 01-infrastructure-shell
    provides: "IUserSessionContext, UserSessionContext, SessionPersistence, multi-tenancy query filters"
provides:
  - "Nullable int? session context types enabling correct query filter bypass when tenant not set"
  - "Null-safe session value display in SessionInfo debug component"
affects: [04-write-operations, 05-localization-ux]

# Tech tracking
tech-stack:
  added: []
  patterns: ["Nullable int? for tenant IDs -- null means no filtering, value means filter"]

key-files:
  created: []
  modified:
    - "src/SignaturPortal.Application/Interfaces/IUserSessionContext.cs"
    - "src/SignaturPortal.Web/Services/UserSessionContext.cs"
    - "src/SignaturPortal.Web/Components/Layout/SessionPersistence.razor"
    - "src/SignaturPortal.Web/Components/Layout/SessionInfo.razor"
    - "tests/SignaturPortal.Tests/Authorization/PermissionServiceTests.cs"

key-decisions:
  - "UserId/SiteId/ClientId changed to int? while UserLanguageId stays int (0 is valid default for language)"
  - "Null fallback instead of 0 ensures query filter bypass (null = no tenant filtering, per DbContext design)"

patterns-established:
  - "Tenant ID nullable pattern: int? where null means unset/bypass, value means active filter"

# Metrics
duration: 5min
completed: 2026-02-15
---

# Phase 3 Plan 5: Nullable Session Context Summary

**Changed IUserSessionContext tenant IDs from int to int? so uninitialized sessions pass null to query filters (bypass) instead of 0 (wrong tenant match)**

## Performance

- **Duration:** 5 min
- **Started:** 2026-02-15T22:15:30Z
- **Completed:** 2026-02-15T22:20:30Z
- **Tasks:** 2
- **Files modified:** 5

## Accomplishments
- IUserSessionContext.UserId, SiteId, ClientId changed from int to int? -- null means "not set", enabling correct query filter bypass
- UserSessionContext.Initialize() defaults to null instead of 0 when session keys are missing
- SessionPersistence.razor correctly round-trips nullable values through PersistentComponentState
- All service methods (ActivityService, PermissionService, UnitOfWork) confirmed compatible -- no code changes needed since DbContext.CurrentSiteId/CurrentClientId were already int?

## Task Commits

Each task was committed atomically:

1. **Task 1: Change IUserSessionContext and UserSessionContext to use nullable int? for tenant IDs** - `cfac5ed` (feat)
2. **Task 2: Fix all service methods that stamp tenant context from session** - No commit needed; verified all int? assignments work without changes

**Plan metadata:** (pending)

## Files Created/Modified
- `src/SignaturPortal.Application/Interfaces/IUserSessionContext.cs` - Changed UserId, SiteId, ClientId from int to int?
- `src/SignaturPortal.Web/Services/UserSessionContext.cs` - Updated properties and Restore() parameters to int?, Initialize() defaults to null
- `src/SignaturPortal.Web/Components/Layout/SessionPersistence.razor` - SessionData class updated with int? types
- `src/SignaturPortal.Web/Components/Layout/SessionInfo.razor` - Null-safe display with "N/A" fallback
- `tests/SignaturPortal.Tests/Authorization/PermissionServiceTests.cs` - TestSessionContext updated for interface compliance

## Decisions Made
- UserId/SiteId/ClientId changed to int? while UserLanguageId stays int -- UserLanguageId defaults to 0 which is a valid default, and it is never used for query filtering
- Null fallback (not 0) ensures DbContext query filters correctly bypass when tenant context is not set, matching the existing `CurrentClientId == null || e.ClientId == CurrentClientId` pattern

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Fixed TestSessionContext in PermissionServiceTests to match updated interface**
- **Found during:** Task 1 (build verification)
- **Issue:** TestSessionContext inner class in unit tests implemented IUserSessionContext with int types, not matching the updated int? interface
- **Fix:** Changed UserId, SiteId, ClientId from int to int? in TestSessionContext
- **Files modified:** tests/SignaturPortal.Tests/Authorization/PermissionServiceTests.cs
- **Verification:** dotnet build tests project passes cleanly
- **Committed in:** cfac5ed (Task 1 commit)

---

**Total deviations:** 1 auto-fixed (1 blocking)
**Impact on plan:** Required fix for test compilation. No scope creep.

## Issues Encountered
- SignaturPortal.Web.exe running in background locked DLLs, preventing full solution build. The Application, Infrastructure, and Tests projects all compiled cleanly. Only the file-copy step to Web's bin folder failed due to process lock. This is an environment issue, not a code issue.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- Nullable session context is the last gap-closure fix for Phase 3 read views
- Activity list should now correctly display data (null ClientId bypasses filter instead of matching ClientId == 0)
- Plan 03-06 (error handling) is the final Phase 3 plan before moving to Phase 4 (Write Operations)

## Self-Check: PASSED

- All 6 referenced files: FOUND
- Commit cfac5ed: FOUND

---
*Phase: 03-core-read-views*
*Completed: 2026-02-15*
