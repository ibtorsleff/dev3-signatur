---
phase: 03-core-read-views
plan: 04
subsystem: Recruitment
tags: [candidates, file-download, mudblazor, blazor-server]
dependency_graph:
  requires:
    - 03-02-activity-list
    - ERCandidate entity
    - ERCandidateFile entity
    - BinaryFile entity
  provides:
    - CandidateListDto
    - CandidateDetailDto
    - CandidateFileDto
    - GetCandidatesAsync
    - GetCandidateDetailAsync
    - GetCandidateFileDataAsync
    - /activities/{id}/candidates page
    - /activities/{id}/candidates/{candidateId} page
    - File download via JS interop
  affects:
    - IActivityService (extended with 3 new methods)
    - StatusMappings (added candidate status mapping)
    - SignalR configuration (increased max message size)
tech_stack:
  added:
    - JavaScript interop (DotNetStreamReference)
  patterns:
    - File download via blob URL
    - Server-side candidate search with debounce
    - Security verification for file access
key_files:
  created:
    - src/SignaturPortal.Application/DTOs/CandidateDetailDto.cs
    - src/SignaturPortal.Application/DTOs/CandidateFileDto.cs
    - src/SignaturPortal.Web/Components/Pages/Activities/CandidateList.razor
    - src/SignaturPortal.Web/Components/Pages/Activities/CandidateList.razor.cs
    - src/SignaturPortal.Web/Components/Pages/Activities/CandidateDetail.razor
    - src/SignaturPortal.Web/Components/Pages/Activities/CandidateDetail.razor.cs
    - src/SignaturPortal.Web/wwwroot/js/fileDownload.js
  modified:
    - src/SignaturPortal.Application/DTOs/CandidateListDto.cs
    - src/SignaturPortal.Application/Interfaces/IActivityService.cs
    - src/SignaturPortal.Infrastructure/Services/ActivityService.cs
    - src/SignaturPortal.Domain/Helpers/StatusMappings.cs
    - src/SignaturPortal.Web/Components/App.razor
    - src/SignaturPortal.Web/Program.cs
decisions:
  - title: Candidate status mapping hardcoded for Phase 3
    rationale: Phase 5 will implement full database-driven localized status lookup
    impact: Status names are currently static English values
  - title: File download via DotNetStreamReference and JS interop
    rationale: Blazor Server requires JS interop to trigger browser file save dialog
    impact: Files downloaded to browser's default download folder
  - title: SignalR max message size set to 10MB
    rationale: Support file downloads up to 10MB (typical for CVs and documents)
    impact: Larger files would require chunking or direct HTTP download endpoint
  - title: File ownership verified before download
    rationale: Security check prevents unauthorized file access across tenants
    impact: Query joins through candidate-activity-tenant chain before loading FileData
metrics:
  duration_minutes: 5
  completed_date: 2026-02-15
  tasks_completed: 2
  files_created: 7
  files_modified: 6
---

# Phase 03 Plan 04: Candidate List and Detail with File Download Summary

**One-liner:** Candidate list and detail pages with server-side search, file download via JS interop, and tenant-scoped file access verification.

## What Was Built

### Application Layer (DTOs + Service Interface)

**CandidateListDto** - Grid display DTO with contact info, status, file count
- Properties: ErcandidateId, EractivityId, FirstName, LastName, FullName (computed), Email, Telephone, City, ZipCode, RegistrationDate, ErcandidateStatusId, StatusName, IsDeleted, FileCount
- Used in candidate list MudDataGrid

**CandidateDetailDto** - Detail page DTO with full contact info and file list
- Properties: All CandidateListDto properties plus Address, DateOfBirth, IsInternalCandidate, LanguageId, Files (List<CandidateFileDto>), ActivityHeadline
- Files list contains metadata only (no binary data)

**CandidateFileDto** - File metadata with formatted size
- Properties: BinaryFileId, FileName, FileSize, FileSizeFormatted (computed: B/KB/MB)
- Used in file attachment list on candidate detail page

**IActivityService Extended** - 3 new methods
- `GetCandidatesAsync(int activityId, GridRequest request)` - Paginated candidate list with name search
- `GetCandidateDetailAsync(int activityId, int candidateId)` - Candidate detail with file metadata
- `GetCandidateFileDataAsync(int candidateId, int binaryFileId)` - Binary file download with ownership verification

### Infrastructure Layer (Service Implementation)

**ActivityService.GetCandidatesAsync**
- Queries ERCandidate filtered by activityId and !IsDeleted
- Tenant isolation via global query filter on activity.ClientId
- Name search: filters on FirstName.Contains() OR LastName.Contains()
- FileCount computed via subquery to ERCandidateFile
- StatusName mapped in-memory via StatusMappings.GetCandidateStatusName()
- Default sort: RegistrationDate descending
- Returns GridResponse<CandidateListDto>

**ActivityService.GetCandidateDetailAsync**
- Queries ERCandidate with security check (both activityId and candidateId must match)
- Loads file metadata via join to ERCandidateFile -> BinaryFile (FileName, FileSize only)
- Loads activity headline for breadcrumbs
- Returns null if not found or cross-tenant access attempt
- StatusName mapped in-memory

**ActivityService.GetCandidateFileDataAsync**
- **Security verification:** Joins ERCandidateFile -> ERCandidate -> ERActivity to verify file ownership
- Global filter on ERActivity ensures tenant isolation
- Loads BinaryFile.FileData ONLY after ownership verified
- Returns (byte[] FileData, string FileName) tuple or null if unauthorized
- Critical: Only loads single file's binary data, never in bulk

**StatusMappings.GetCandidateStatusName**
- Added candidate status dictionary with placeholder values (1=Registered, 2=Under Review, 3=Interview, 4=Hired, 5=Rejected)
- TODO Phase 5: Replace with database-driven localized lookup

### Web Layer (Blazor Pages)

**fileDownload.js** - JavaScript interop for file download
- `downloadFileFromStream(fileName, contentStreamReference)` function
- Creates blob URL from stream, triggers anchor click, cleans up
- Included in App.razor script references

**CandidateList.razor** - Route: `/activities/{id}/candidates`
- Authorization: RecruitmentAccess policy
- Search field with 300ms debounce, triggers server reload
- MudDataGrid with columns: FullName, Email, Telephone, City, RegistrationDate, StatusName, FileCount
- Click row or View icon to navigate to candidate detail
- Server-side data loading, pagination, sorting
- Breadcrumbs: Activities > Activity > Candidates

**CandidateList.razor.cs** - Code-behind
- Injects IActivityService, NavigationManager
- LoadServerData converts MudBlazor GridState to Application GridRequest
- Adds FullName filter if search string not empty
- OnSearchChanged triggers grid reload
- NavigateToDetail navigates to `/activities/{id}/candidates/{candidateId}`

**CandidateDetail.razor** - Route: `/activities/{id}/candidates/{candidateId}`
- Authorization: RecruitmentAccess policy
- Layout: 2-column grid (contact info card + attachments card)
- Contact info: Email, Phone, Address, City, DOB, Applied date, Internal candidate flag
- Attachments: File list with name, formatted size, download button
- Download button shows loading spinner while downloading
- Loading and not-found states handled
- Breadcrumbs: Activities > Activity Headline > Candidates > Candidate Name

**CandidateDetail.razor.cs** - Code-behind
- Injects IActivityService, IJSRuntime, ISnackbar, NavigationManager
- OnInitializedAsync loads candidate detail and builds breadcrumbs
- DownloadFileAsync:
  1. Calls ActivityService.GetCandidateFileDataAsync
  2. Creates MemoryStream from byte[]
  3. Creates DotNetStreamReference
  4. Invokes JS interop `downloadFileFromStream`
  5. Shows success/error snackbar
  6. Prevents concurrent downloads with `_downloadingFileId` flag

**Program.cs** - SignalR Configuration
- Added `.AddHubOptions()` to InteractiveServerComponents chain
- Set `MaximumReceiveMessageSize = 10 * 1024 * 1024` (10 MB)
- Required for file downloads over SignalR

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] MudBlazor 8.0.0 generic type inference**
- **Found during:** Task 2 build
- **Issue:** MudChip, MudList, MudListItem require explicit T parameter in MudBlazor 8.0.0
- **Fix:** Added T="string" to MudChip, MudList, MudListItem components
- **Files modified:** CandidateDetail.razor
- **Commit:** 83f5666

**2. [Rule 1 - Bug] MudDataGrid Sortable property deprecated**
- **Found during:** Task 2 build
- **Issue:** MudBlazor analyzer warning MUD0002 - Sortable property deprecated
- **Fix:** Changed Sortable="true" to SortMode="SortMode.Single"
- **Files modified:** CandidateList.razor
- **Commit:** 83f5666

## Verification Results

1. `dotnet build SignaturPortal.slnx` - 0 errors (3 warnings: NU1603 MudBlazor version, TUnit assertion)
2. IActivityService has GetCandidatesAsync, GetCandidateDetailAsync, GetCandidateFileDataAsync methods
3. ActivityService implements all three methods with tenant-scoped queries
4. GetCandidateFileDataAsync verifies file ownership through candidate-activity-tenant chain before loading binary data
5. CandidateList.razor route is `/activities/{ActivityId:int}/candidates`
6. CandidateDetail.razor route is `/activities/{ActivityId:int}/candidates/{CandidateId:int}`
7. fileDownload.js exists at wwwroot/js/fileDownload.js
8. App.razor includes script reference
9. Search field triggers server data reload with 300ms debounce
10. DownloadFileAsync method uses DotNetStreamReference for JS interop

## Key Technical Details

**File Download Security Chain**
1. User clicks download button with candidateId and binaryFileId
2. GetCandidateFileDataAsync queries ERCandidateFile WHERE candidateId = X AND binaryFileId = Y
3. Join to ERCandidate -> ERActivity to get ActivityClientId
4. Global filter on ERActivity ensures ClientId matches user's session
5. If ownership verified, load BinaryFile.FileData for that specific file
6. Return null if file doesn't exist or belongs to different tenant
7. Web layer creates DotNetStreamReference from byte[]
8. JS interop creates blob URL and triggers browser download
9. Cleanup: revoke blob URL after download

**Name Search Implementation**
- GridRequest.Filters contains FilterDefinition("FullName", "contains", searchTerm)
- ActivityService checks for filter on "FullName", "FirstName", or "LastName"
- Applies WHERE clause: `c.FirstName.Contains(searchTerm) OR c.LastName.Contains(searchTerm)`
- Search is case-sensitive at database level (SQL Server default collation)
- TODO Phase 5: Consider case-insensitive search with COLLATE or EF Core functions

**MudBlazor 8.0.0 Generic Types**
- MudChip<T>, MudList<T>, MudListItem<T> require explicit T parameter
- For display-only components without binding, T="string" is sufficient
- MudDataGrid<T> infers from ServerData callback return type

## Self-Check: PASSED

### Files Created
- [FOUND] src/SignaturPortal.Application/DTOs/CandidateDetailDto.cs
- [FOUND] src/SignaturPortal.Application/DTOs/CandidateFileDto.cs
- [FOUND] src/SignaturPortal.Web/Components/Pages/Activities/CandidateList.razor
- [FOUND] src/SignaturPortal.Web/Components/Pages/Activities/CandidateList.razor.cs
- [FOUND] src/SignaturPortal.Web/Components/Pages/Activities/CandidateDetail.razor
- [FOUND] src/SignaturPortal.Web/Components/Pages/Activities/CandidateDetail.razor.cs
- [FOUND] src/SignaturPortal.Web/wwwroot/js/fileDownload.js

### Commits
- [FOUND] 9a29d28: feat(03-04): add candidate DTOs and service methods
- [FOUND] 83f5666: feat(03-04): add candidate list and detail pages with file download

### Build Verification
- [PASSED] dotnet build SignaturPortal.slnx - 0 errors
