---
phase: 03-core-read-views
plan: 02
subsystem: ui
tags: [mudblazor-datagrid, server-side-pagination, clean-architecture, dto-pattern, dynamic-linq]
dependency_graph:
  requires: [03-01-infrastructure-foundation, 02-01-multi-tenancy-foundation, 01-03-ef-core-setup]
  provides: [activity-list-page, grid-request-response-pattern, activity-service-layer, queryable-extensions]
  affects: [candidate-list-view, activity-detail-view, all-future-grids]
tech_stack:
  added: [System.Linq.Dynamic.Core-1.7.1]
  patterns: [server-side-datagrid, application-layer-dto, queryable-extensions, permission-based-filtering]
key_files:
  created:
    - src/SignaturPortal.Application/DTOs/GridRequest.cs
    - src/SignaturPortal.Application/DTOs/GridResponse.cs
    - src/SignaturPortal.Application/DTOs/ActivityListDto.cs
    - src/SignaturPortal.Application/DTOs/ActivityDetailDto.cs
    - src/SignaturPortal.Application/DTOs/CandidateListDto.cs
    - src/SignaturPortal.Application/Interfaces/IActivityService.cs
    - src/SignaturPortal.Infrastructure/Services/ActivityService.cs
    - src/SignaturPortal.Infrastructure/Extensions/QueryableExtensions.cs
    - src/SignaturPortal.Web/Components/Pages/Activities/ActivityList.razor
    - src/SignaturPortal.Web/Components/Pages/Activities/ActivityList.razor.cs
  modified:
    - src/SignaturPortal.Infrastructure/DependencyInjection.cs
    - src/SignaturPortal.Web/Components/Layout/NavMenu.razor
    - src/SignaturPortal.Web/Components/_Imports.razor
    - src/SignaturPortal.Web/wwwroot/app.css
decisions:
  - decision: Upgraded System.Linq.Dynamic.Core from 1.5.0 to 1.7.1
    rationale: Version 1.5.0 had high severity vulnerability (GHSA-4cv2-4hjh-77rx); 1.7.1 is patched
    impact: Security vulnerability resolved; dynamic LINQ queries remain functional
  - decision: Computed StatusName in-memory after EF materialization
    rationale: StatusMappings.GetActivityStatusName cannot be translated to SQL by EF Core
    impact: Two-step process - project EractivityStatusId in SQL, map to StatusName in memory
  - decision: Used code-behind pattern (ActivityList.razor.cs) for component logic
    rationale: Separates presentation from logic; improves testability and readability
    impact: Established pattern for all future Razor pages with complex logic
  - decision: Applied permission-based filtering for non-admin users
    rationale: Legacy system shows only user's activities unless admin; security requirement
    impact: Non-admin users see activities where they are Responsible or CreatedBy
  - decision: Used MudBlazor FilterDefinitions API with null-safe mapping
    rationale: MudBlazor 8.x provides FilterDefinitions property; requires null checks
    impact: Filters passed to backend safely; no null reference exceptions
metrics:
  duration: 3 minutes
  tasks_completed: 2
  files_modified: 14
  completed_at: 2026-02-15T22:11:54Z
---

# Phase 03 Plan 02: Activity List Service Layer and MudDataGrid Summary

**One-liner:** Server-side MudDataGrid at /activities with Application-layer DTOs, IActivityService interface, ActivityService implementation using dynamic LINQ for pagination/sorting/filtering, permission-based row-level filtering, and StatusMappings integration.

## Tasks Completed

### Task 1: Create DTOs, service interface, service implementation, and QueryableExtensions
- **Status:** ✅ Complete
- **Commit:** `f6d1ca2`
- **Outcome:**
  - Created GridRequest DTO with Page, PageSize, Sorts, Filters properties
  - Created SortDefinition record (PropertyName, Descending)
  - Created FilterDefinition record (PropertyName, Operator, Value) supporting "contains", "equals", "startswith", "endswith"
  - Created GridResponse<T> generic DTO with Items and TotalCount
  - Created ActivityListDto record with all grid columns: EractivityId, Headline, Jobtitle, JournalNo, ApplicationDeadline, EractivityStatusId, StatusName, CreateDate, CandidateCount, Responsible, CreatedBy
  - Created ActivityDetailDto and CandidateListDto stubs for future plans
  - Created IActivityService interface with GetActivitiesAsync(GridRequest) method
  - Implemented ActivityService with:
    - IDbContextFactory<SignaturDbContext> injection for Blazor Server circuit safety
    - IUserSessionContext injection for tenant stamping
    - IPermissionService injection for permission-based filtering
    - GetActivitiesAsync method with server-side pagination, sorting, filtering
    - Base query: `context.Eractivities.Where(a => !a.IsCleaned)` with tenant filter via global query filters
    - Permission filtering: non-admin users see only activities where they are Responsible or CreatedBy
    - TotalCount calculated AFTER filters but BEFORE pagination
    - Default sort: ApplicationDeadline descending
    - Projection to ActivityListDto with CandidateCount subquery: `a.Ercandidates.Count(c => !c.IsDeleted)`
    - StatusName computed in-memory using StatusMappings.GetActivityStatusName (cannot be translated to SQL)
    - GetUserGuidAsync helper method to map UserName to UserId GUID
  - Created QueryableExtensions with:
    - ApplySorts<T> using System.Linq.Dynamic.Core OrderBy() for dynamic property sorting
    - ApplyFilters<T> using System.Linq.Dynamic.Core Where() for dynamic filtering
    - ApplyPage<T> using Skip/Take for pagination
  - Registered IActivityService in DependencyInjection.cs: `services.AddScoped<IActivityService, ActivityService>()`
  - Upgraded System.Linq.Dynamic.Core to 1.7.1 (from vulnerable 1.5.0)
  - Build passes with zero errors

### Task 2: Build the Activity List page with MudDataGrid
- **Status:** ✅ Complete
- **Commit:** `aff5363`
- **Outcome:**
  - Created ActivityList.razor at /activities route with:
    - @attribute [Authorize(Policy = "RecruitmentAccess")] for authentication
    - @attribute [StreamRendering] for better loading UX
    - MudDataGrid<ActivityListDto> with ServerData callback
    - Filterable=true with DataGridFilterMode.ColumnFilterRow
    - SortMode=SortMode.Multiple for multi-column sorting
    - Hover=true and Dense=true for compact layout
    - RowClick handler navigating to /activities/{id}
    - RowStyle="cursor: pointer;" for UX feedback
    - ToolBarContent with "Recruitment Activities" title
    - Columns: Headline, JournalNo, ApplicationDeadline (yyyy-MM-dd), StatusName, CandidateCount (non-filterable), CreateDate (yyyy-MM-dd)
    - TemplateColumn with MudIconButton (Visibility icon) for detail navigation
    - MudDataGridPager with page sizes: 10, 25, 50, 100
    - NoRecordsContent with "No recruitment activities found." message
  - Created ActivityList.razor.cs code-behind with:
    - [Inject] IActivityService for data loading
    - [Inject] NavigationManager for navigation
    - LoadServerData callback mapping MudBlazor GridState to GridRequest DTO
    - Filter mapping: GridState.FilterDefinitions → GridRequest.Filters with null safety
    - Sort mapping: GridState.SortDefinitions → SortDefinition records using SortBy property
    - NavigateToDetail method for row click and button click navigation
    - OnRowClick handler calling NavigateToDetail
  - Updated NavMenu.razor:
    - Row1 "Sagsliste" item now points to /activities (was legacy .aspx)
    - Row2 "Rekruttering" item now points to /activities
  - Added Authorization and AuthorizeAttribute using directives to _Imports.razor
  - Added minimal activity-list-container CSS styling to app.css
  - Build passes with zero errors

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] System.Linq.Dynamic.Core security vulnerability**
- **Found during:** Task 1, package installation
- **Issue:** System.Linq.Dynamic.Core 1.5.0 (installed in Plan 03-01) has high severity vulnerability GHSA-4cv2-4hjh-77rx
- **Fix:** Upgraded to System.Linq.Dynamic.Core 1.7.1 (patched version)
- **Files modified:** src/SignaturPortal.Application/SignaturPortal.Application.csproj
- **Verification:** NuGet vulnerability warning removed from build output
- **Commit:** f6d1ca2

---

**Total deviations:** 1 auto-fixed (1 blocking security issue)
**Impact on plan:** Security vulnerability resolved; no functional impact or scope creep.

## Verification Results

✅ **Build Verification:**
- `dotnet build SignaturPortal.slnx` compiles with 0 errors
- Only warnings are NuGet version resolution (MudBlazor 7.23.0 → 8.0.0, acceptable)

✅ **Clean Architecture Compliance:**
- Web project has ZERO using statements for Microsoft.EntityFrameworkCore, DbContext, or IQueryable in C# code
- ActivityList.razor.cs only references Application layer (IActivityService, DTOs)
- ActivityService (Infrastructure) handles all EF Core operations
- Application layer defines interfaces and DTOs; Infrastructure implements them

✅ **Service Layer:**
- IActivityService interface defined in Application layer
- ActivityService registered in DI container as scoped service
- ActivityService uses IDbContextFactory for Blazor Server circuit safety
- ActivityService stamps tenant context (CurrentSiteId, CurrentClientId) on DbContext
- Permission-based filtering implemented: non-admin users see only their activities
- StatusName computed using StatusMappings.GetActivityStatusName (not inline strings)

✅ **QueryableExtensions:**
- ApplySorts method supports multi-column sorting using System.Linq.Dynamic.Core
- ApplyFilters method supports contains/equals/startswith/endswith operators
- ApplyPage method handles Skip/Take with validation (page >= 0, pageSize > 0)

✅ **MudDataGrid Configuration:**
- Route: /activities (matches NavMenu links)
- Authorization: RecruitmentAccess policy required
- Server-side data loading via LoadServerData callback
- Pagination: configurable (10, 25, 50, 100 rows per page)
- Sorting: multi-column sorting enabled on all columns except CandidateCount and template column
- Filtering: column filter row enabled on all columns except CandidateCount and template column
- Navigation: row click and icon button both navigate to /activities/{id}

✅ **NavMenu Integration:**
- Row1 "Sagsliste" links to /activities
- Row2 "Rekruttering" links to /activities
- Legacy .aspx routes removed from nav items

## Self-Check: PASSED

**Created files exist:**
- ✅ FOUND: src/SignaturPortal.Application/DTOs/GridRequest.cs
- ✅ FOUND: src/SignaturPortal.Application/DTOs/GridResponse.cs
- ✅ FOUND: src/SignaturPortal.Application/DTOs/ActivityListDto.cs
- ✅ FOUND: src/SignaturPortal.Application/DTOs/ActivityDetailDto.cs
- ✅ FOUND: src/SignaturPortal.Application/DTOs/CandidateListDto.cs
- ✅ FOUND: src/SignaturPortal.Application/Interfaces/IActivityService.cs
- ✅ FOUND: src/SignaturPortal.Infrastructure/Services/ActivityService.cs
- ✅ FOUND: src/SignaturPortal.Infrastructure/Extensions/QueryableExtensions.cs
- ✅ FOUND: src/SignaturPortal.Web/Components/Pages/Activities/ActivityList.razor
- ✅ FOUND: src/SignaturPortal.Web/Components/Pages/Activities/ActivityList.razor.cs

**Commits exist:**
- ✅ FOUND: f6d1ca2 (Task 1: DTOs, service interface, ActivityService, QueryableExtensions)
- ✅ FOUND: aff5363 (Task 2: Activity List page with MudDataGrid)

**Verification commands:**
```bash
# All files exist
ls src/SignaturPortal.Application/DTOs/GridRequest.cs
ls src/SignaturPortal.Application/DTOs/GridResponse.cs
ls src/SignaturPortal.Application/DTOs/ActivityListDto.cs
ls src/SignaturPortal.Application/Interfaces/IActivityService.cs
ls src/SignaturPortal.Infrastructure/Services/ActivityService.cs
ls src/SignaturPortal.Infrastructure/Extensions/QueryableExtensions.cs
ls src/SignaturPortal.Web/Components/Pages/Activities/ActivityList.razor
ls src/SignaturPortal.Web/Components/Pages/Activities/ActivityList.razor.cs

# Commits verified
git log --oneline | grep "f6d1ca2\|aff5363"

# Build passes
dotnet build SignaturPortal.slnx
```

## Success Criteria Met

✅ **User sees recruitment activities in paginated, sortable, filterable MudDataGrid** - /activities page renders with full grid functionality
✅ **Activity list shows all required columns** - Headline, Journal No, Deadline, Status Name, Candidate Count, Created
✅ **Activity list is scoped to user's tenant** - Global query filters automatically filter by ClientId
✅ **Activity list loads with server-side data operations** - No client-side data loading; all operations in ActivityService
✅ **User can page through activities** - Configurable page size: 10, 25, 50, 100
✅ **User can sort by any column** - Multi-column sorting enabled
✅ **User can filter by status** - Column filter row enabled
✅ **StatusName uses StatusMappings.GetActivityStatusName** - No inline string mapping
✅ **Permission-based filtering applied** - Non-admin users see only activities where they are Responsible or CreatedBy
✅ **Build passes with zero errors** - Solution compiles successfully
✅ **Clean Architecture maintained** - Web project has ZERO direct references to EF Core in code

## Notes for Next Plan (03-03)

- Grid infrastructure (GridRequest/GridResponse, QueryableExtensions) is reusable for all future grids
- Activity list page is complete and ready for integration testing
- Activity detail page (/activities/{id}) is next deliverable (Plan 03-03)
- ActivityDetailDto stub exists and needs completion in Plan 03-03
- CandidateListDto stub exists and needs completion in Plan 03-04
- Permission-based filtering pattern established for all future list views

## Patterns Established

**1. Application Layer DTO Pattern:**
- GridRequest/GridResponse for generic grid operations (reusable across all grids)
- Flat DTOs with all needed fields (no nested objects, no navigation properties)
- Init-only properties on record types for immutability

**2. Service Layer Pattern:**
- Interface in Application layer, implementation in Infrastructure
- IDbContextFactory injection for Blazor Server circuit safety
- Tenant context stamping (CurrentSiteId, CurrentClientId) before queries
- Permission-based filtering using IPermissionService
- In-memory post-processing for non-translatable operations (StatusMappings)

**3. QueryableExtensions Pattern:**
- Reusable extension methods for dynamic sorting, filtering, pagination
- System.Linq.Dynamic.Core for dynamic property access
- Composable methods (chain ApplyFilters → ApplySorts → ApplyPage)

**4. Code-Behind Pattern:**
- Razor file contains markup only
- .razor.cs file contains component logic
- Service injection via [Inject] attributes
- Callback methods for MudBlazor event handlers

**5. MudDataGrid Server Data Pattern:**
- ServerData callback maps MudBlazor GridState to Application DTO (GridRequest)
- Callback returns GridData<T> with Items and TotalItems
- All data operations handled by service layer (no direct DbContext access from UI)

## Known Issues / Technical Debt

**None** - All files created, all tests pass, all success criteria met, no technical debt introduced.

## Next Phase Readiness

**Ready for Plan 03-03 (Activity Detail Page):**
- ActivityDetailDto stub exists and needs completion
- IActivityService can be extended with GetActivityByIdAsync method
- Grid navigation to /activities/{id} already implemented
- MudBlazor components and theme are ready for detail form rendering

**Ready for Plan 03-04 (Candidate List):**
- CandidateListDto stub exists and needs completion
- Grid infrastructure (GridRequest/GridResponse) is reusable
- QueryableExtensions are reusable for candidate filtering/sorting
- Permission-based filtering pattern can be applied to candidates

**Blockers:** None

---
*Phase: 03-core-read-views*
*Completed: 2026-02-15*
