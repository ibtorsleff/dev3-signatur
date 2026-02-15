---
phase: 03-core-read-views
plan: 06
subsystem: ui
tags: [blazor-server, error-handling, mudblazor, circuit-resilience, snackbar]

# Dependency graph
requires:
  - phase: 03-core-read-views
    provides: "Activity and candidate pages (plans 01-04)"
provides:
  - "Try-catch error handling in all activity/candidate page lifecycle methods"
  - "Graceful error display via MudAlert and ISnackbar"
  - "Circuit-safe data grid callbacks with empty fallback on error"
affects: [04-write-operations, 05-localization-ux]

# Tech tracking
tech-stack:
  added: []
  patterns: ["try-catch in OnInitializedAsync with _errorMessage + _notFound state", "try-catch in LoadServerData returning empty GridData on error", "ISnackbar for non-blocking error notifications in list pages"]

key-files:
  created: []
  modified:
    - src/SignaturPortal.Web/Components/Pages/Activities/ActivityDetail.razor
    - src/SignaturPortal.Web/Components/Pages/Activities/ActivityDetail.razor.cs
    - src/SignaturPortal.Web/Components/Pages/Activities/CandidateDetail.razor
    - src/SignaturPortal.Web/Components/Pages/Activities/CandidateDetail.razor.cs
    - src/SignaturPortal.Web/Components/Pages/Activities/ActivityList.razor.cs
    - src/SignaturPortal.Web/Components/Pages/Activities/CandidateList.razor.cs

key-decisions:
  - "Used _errorMessage field (not ILogger) for error display -- Phase 5 will add proper logging infrastructure"
  - "Error in detail pages sets _notFound + _errorMessage so existing not-found UI still works"
  - "Error in list pages returns empty GridData and shows Snackbar instead of MudAlert"

patterns-established:
  - "Detail page error pattern: try-catch in OnInitializedAsync sets _notFound + _errorMessage, razor shows MudAlert Severity.Error"
  - "List page error pattern: try-catch in LoadServerData returns empty GridData + Snackbar.Add with Severity.Error"

# Metrics
duration: 4min
completed: 2026-02-15
---

# Phase 3 Plan 6: Error Handling Summary

**Try-catch error handling in all 4 activity/candidate Blazor pages to prevent Blazor circuit death on service exceptions**

## Performance

- **Duration:** 4 min
- **Started:** 2026-02-15T22:15:40Z
- **Completed:** 2026-02-15T22:19:53Z
- **Tasks:** 2
- **Files modified:** 6

## Accomplishments
- ActivityDetail and CandidateDetail lifecycle methods wrapped in try-catch with user-friendly MudAlert error display
- ActivityList and CandidateList server data callbacks wrapped in try-catch with ISnackbar error notifications and empty grid fallback
- Blazor SignalR circuit no longer dies when navigating to invalid IDs or when service layer throws exceptions

## Task Commits

Each task was committed atomically:

1. **Task 1: Add try-catch to ActivityDetail and CandidateDetail lifecycle methods** - `6f7f553` (fix)
2. **Task 2: Add defensive error handling to ActivityList and CandidateList data callbacks** - `4880c27` (fix)

## Files Created/Modified
- `src/SignaturPortal.Web/Components/Pages/Activities/ActivityDetail.razor` - Added MudAlert Severity.Error branch for _errorMessage
- `src/SignaturPortal.Web/Components/Pages/Activities/ActivityDetail.razor.cs` - Added _errorMessage field, try-catch around OnInitializedAsync service call
- `src/SignaturPortal.Web/Components/Pages/Activities/CandidateDetail.razor` - Added MudAlert Severity.Error branch for _errorMessage, updated null check to include _notFound
- `src/SignaturPortal.Web/Components/Pages/Activities/CandidateDetail.razor.cs` - Added _notFound and _errorMessage fields, try-catch around OnInitializedAsync service call
- `src/SignaturPortal.Web/Components/Pages/Activities/ActivityList.razor.cs` - Added ISnackbar injection, try-catch in LoadServerData returning empty GridData
- `src/SignaturPortal.Web/Components/Pages/Activities/CandidateList.razor.cs` - Added ISnackbar injection, try-catch in LoadServerData returning empty GridData

## Decisions Made
- Used `_errorMessage` field pattern (not ILogger) for error display -- Phase 5 will add proper logging infrastructure
- Error in detail pages sets both `_notFound` and `_errorMessage` so existing not-found UI path still works as fallback
- Error in list pages returns empty `GridData` and shows `Snackbar` instead of `MudAlert` (non-blocking, dismissible)
- CandidateDetail already had `_loading` state -- added `_notFound` separately to distinguish null-candidate from error scenarios

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered
- Build verification required temp output directory (`-o build-temp`) because a running SignaturPortal.Web.exe process locked the bin/Debug DLLs. Build succeeded with 0 errors using this approach.
- Pre-existing test compilation error in `PermissionServiceTests.cs` (unrelated to this plan) -- `TestSessionContext` interface mismatch from a previous uncommitted change.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness
- All Phase 3 read views now have defensive error handling -- circuits survive invalid IDs and service failures
- Ready for Phase 4 write operations which will follow the same error handling patterns established here
- Phase 5 should add ILogger-based structured logging to replace the current swallow-and-display pattern

## Self-Check: PASSED

All 6 modified files verified present on disk. Both task commits (`6f7f553`, `4880c27`) verified in git log.

---
*Phase: 03-core-read-views*
*Completed: 2026-02-15*
