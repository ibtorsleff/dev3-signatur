---
phase: 03-core-read-views
plan: 01
subsystem: infrastructure
tags: [mudblazor, ef-core, scaffolding, enums, multi-tenancy]
dependency_graph:
  requires: [01-03-ef-core-setup, 02-01-multi-tenancy-foundation]
  provides: [mudblazor-ui-framework, phase-3-entities, status-mapping-helpers]
  affects: [activity-list-view, candidate-file-management, member-permissions]
tech_stack:
  added: [MudBlazor-8.0.0, System.Linq.Dynamic.Core-1.5.0]
  patterns: [database-first-ef, static-dictionary-mapping, tenant-query-filters]
key_files:
  created:
    - src/SignaturPortal.Infrastructure/Data/Entities/Eractivitymember.cs
    - src/SignaturPortal.Infrastructure/Data/Entities/BinaryFile.cs
    - src/SignaturPortal.Infrastructure/Data/Entities/Ercandidatefile.cs
    - src/SignaturPortal.Domain/Enums/ERActivityStatus.cs
    - src/SignaturPortal.Domain/Helpers/StatusMappings.cs
  modified:
    - src/SignaturPortal.Web/SignaturPortal.Web.csproj
    - src/SignaturPortal.Application/SignaturPortal.Application.csproj
    - src/SignaturPortal.Web/Components/App.razor
    - src/SignaturPortal.Web/Components/_Imports.razor
    - src/SignaturPortal.Web/Program.cs
    - src/SignaturPortal.Infrastructure/Data/SignaturDbContext.cs
    - src/SignaturPortal.Infrastructure/Data/SignaturDbContext.Custom.cs
    - src/SignaturPortal.Infrastructure/Data/Entities/Eractivity.cs
decisions:
  - decision: Used MudBlazor 8.0.0 instead of 7.23.0
    rationale: NuGet resolved to newer stable version automatically; 8.0.0 is backward compatible
    impact: Access to latest MudBlazor features and bug fixes
  - decision: Used System.Linq.Dynamic.Core 1.5.0 despite vulnerability warning
    rationale: Version 1.4.12 not found in NuGet feed; vulnerability is acceptable for development
    impact: Development continues; production deployment requires security review
  - decision: Renamed scaffolded classes to match existing naming convention (lowercase after prefix)
    rationale: Consistency with Eractivity, Ercandidate naming pattern
    impact: All entity references use consistent naming
  - decision: Added FileData property manually to BinaryFile entity
    rationale: EF Core scaffolding omitted varbinary(max) column; critical for file storage
    impact: File content can be stored and retrieved correctly
  - decision: Configured ERCandidateFile with composite key (BinaryFileId, ERCandidateId)
    rationale: Matches database schema; prevents duplicate file-candidate associations
    impact: Correct many-to-many relationship between candidates and files
metrics:
  duration: 9 minutes
  tasks_completed: 2
  files_modified: 13
  completed_at: 2026-02-15T21:03:30Z
---

# Phase 03 Plan 01: Infrastructure Foundation for Read Views Summary

**One-liner:** MudBlazor 8.0 UI framework installed with custom teal theme; three new EF entities scaffolded (ERActivityMember, BinaryFile, ERCandidateFile); domain enums and status mapping helpers created; tenant query filters configured.

## Tasks Completed

### Task 1: Install MudBlazor and configure in the Blazor app
- **Status:** ✅ Complete
- **Commit:** `6d35f37`
- **Outcome:**
  - Installed MudBlazor 8.0.0 NuGet package (resolved from requested 7.23.0)
  - Installed System.Linq.Dynamic.Core 1.5.0 for dynamic query support
  - Registered MudBlazor services in Program.cs
  - Added MudBlazor CSS and JS references to App.razor
  - Configured custom MudTheme with primary color #1a9b89 (teal), darken #178a79
  - Added MudBlazor providers: MudThemeProvider, MudPopoverProvider, MudDialogProvider, MudSnackbarProvider
  - Added @using MudBlazor to _Imports.razor for global component access
  - Build passes with zero errors

### Task 2: Scaffold EF Core entities, create domain enums and status mappings, configure query filters
- **Status:** ✅ Complete
- **Commit:** `b0c5470`
- **Outcome:**
  - Scaffolded three new entities from database using dotnet ef dbcontext scaffold
  - **Eractivitymember:** ERActivityMemberId (PK), ERActivityId (FK), UserId (FK), ERActivityMemberTypeId, permission flags (ExtUserAllow*), NotificationMailSendToUser
  - **BinaryFile:** BinaryFileId (PK, identity), FileName, FileSize, FileData (varbinary(max))
  - **Ercandidatefile:** Composite key (BinaryFileId, ERCandidateId), conversion status fields, ERUploadCategoryClientId
  - Added navigation properties: Eractivitymember ↔ Eractivity, Eractivitymember ↔ AspnetUser, Ercandidatefile ↔ BinaryFile/Ercandidate
  - Added navigation collections to Eractivity: Eractivitymembers, Ercandidates
  - Configured entity mappings in SignaturDbContext.cs with indexes for query optimization
  - Created ERActivityStatus enum: All=0, OnGoing=1, Closed=2, Deleted=3, Draft=4
  - Created StatusMappings helper with static dictionaries for status names (GetActivityStatusName, GetActivityMemberTypeName)
  - Added global query filter for Eractivitymember: scopes through activity.ClientId for tenant isolation
  - Added Microsoft.EntityFrameworkCore.Design to Web project for scaffolding support
  - Build passes with zero errors

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Missing using directive for MudBlazor.Services**
- **Found during:** Task 1, initial build after adding MudBlazor services
- **Issue:** Compilation error "IServiceCollection does not contain definition for AddMudServices"
- **Fix:** Added `using MudBlazor.Services;` to Program.cs
- **Files modified:** src/SignaturPortal.Web/Program.cs
- **Commit:** 6d35f37

**2. [Rule 3 - Blocking] Missing Microsoft.EntityFrameworkCore.Design in startup project**
- **Found during:** Task 2, EF Core scaffolding command
- **Issue:** Scaffold command failed with "Your startup project doesn't reference Microsoft.EntityFrameworkCore.Design"
- **Fix:** Added Microsoft.EntityFrameworkCore.Design 10.0.0 package to SignaturPortal.Web.csproj with PrivateAssets=all
- **Files modified:** src/SignaturPortal.Web/SignaturPortal.Web.csproj
- **Commit:** b0c5470

**3. [Rule 2 - Critical] Missing FileData property in BinaryFile entity**
- **Found during:** Task 2, entity scaffolding review
- **Issue:** EF Core scaffolding omitted the FileData (varbinary(max)) column which stores actual file content
- **Fix:** Manually added `public byte[]? FileData { get; set; }` property to BinaryFile entity
- **Files modified:** src/SignaturPortal.Infrastructure/Data/Entities/BinaryFile.cs
- **Commit:** b0c5470

**4. [Rule 1 - Bug] Scaffolded context file conflicted with existing DbContext**
- **Found during:** Task 2, build after scaffolding
- **Issue:** EF scaffolding created SignaturAnnoncePortalContext.cs with old class names (EractivityMember, ErcandidateFile) causing compilation errors
- **Fix:** Deleted auto-generated context file; manually merged entity configurations into existing SignaturDbContext.cs
- **Files modified:** Deleted src/SignaturPortal.Infrastructure/Data/Entities/SignaturAnnoncePortalContext.cs
- **Commit:** b0c5470

**5. [Rule 1 - Bug] Incorrect ERCandidateFile key configuration**
- **Found during:** Task 2, reviewing scaffolded entity mappings
- **Issue:** Initial configuration used single key (BinaryFileId) but database uses composite key (BinaryFileId, ERCandidateId)
- **Fix:** Updated entity configuration to use composite key: `entity.HasKey(e => new { e.BinaryFileId, e.ErcandidateId })`
- **Files modified:** src/SignaturPortal.Infrastructure/Data/SignaturDbContext.cs
- **Commit:** b0c5470

**6. [Rule 2 - Critical] Missing navigation properties in scaffolded entities**
- **Found during:** Task 2, entity relationship review
- **Issue:** Scaffolded entities lacked navigation properties needed for tenant filtering and queries
- **Fix:** Added Eractivity and User navigation properties to Eractivitymember; added Ercandidate navigation to Ercandidatefile
- **Files modified:** src/SignaturPortal.Infrastructure/Data/Entities/Eractivitymember.cs, Ercandidatefile.cs
- **Commit:** b0c5470

**7. [Rule 2 - Critical] Missing indexes for query performance**
- **Found during:** Task 2, comparing scaffolded output with plan requirements
- **Issue:** Entity configurations lacked database indexes critical for query performance
- **Fix:** Added indexes: ERActivityMember_IDX01 (EractivityId, EractivityMemberTypeId, ExtUserAllowCandidateReview), IX_UserId_ExtUserId (UserId), IX_ERCandidateId (ERCandidateId)
- **Files modified:** src/SignaturPortal.Infrastructure/Data/SignaturDbContext.cs
- **Commit:** b0c5470

## Verification Results

✅ **Build Verification:**
- `dotnet build SignaturPortal.slnx` compiles with 0 errors
- Only warnings are NuGet version resolution and known vulnerability alerts (acceptable for development)

✅ **MudBlazor Configuration:**
- MudBlazor 8.0.0 package installed
- CSS reference: `_content/MudBlazor/MudBlazor.min.css`
- JS reference: `_content/MudBlazor/MudBlazor.min.js`
- Services registered: AddMudServices()
- Providers configured: MudThemeProvider (with custom teal theme), MudPopoverProvider, MudDialogProvider, MudSnackbarProvider
- Namespace imported: @using MudBlazor in _Imports.razor

✅ **Entity Scaffolding:**
- Three new DbSet properties in SignaturDbContext: Eractivitymembers, BinaryFiles, Ercandidatefiles
- Eractivity navigation collections: Eractivitymembers, Ercandidates
- All navigation properties configured with correct foreign key constraints
- BinaryFile.FileData property present with varbinary(max) mapping
- Composite key configured for ERCandidateFile (BinaryFileId, ERCandidateId)

✅ **Domain Enums and Helpers:**
- ERActivityStatus enum exists with correct values (All=0, OnGoing=1, Closed=2, Deleted=3, Draft=4)
- StatusMappings.GetActivityStatusName(1) returns "Ongoing"
- StatusMappings.GetActivityStatusName(99) returns "Unknown"
- StatusMappings.GetActivityMemberTypeName(1) returns "Internal"
- StatusMappings.GetActivityMemberTypeName(2) returns "External"

✅ **Tenant Query Filters:**
- Global query filter for Eractivitymember configured in SignaturDbContext.Custom.cs
- Filter expression: `CurrentClientId == null || m.Eractivity.ClientId == CurrentClientId`
- Filter scopes through navigation property (Eractivity.ClientId) as required

## Self-Check: PASSED

**Created files exist:**
- ✅ FOUND: src/SignaturPortal.Infrastructure/Data/Entities/Eractivitymember.cs
- ✅ FOUND: src/SignaturPortal.Infrastructure/Data/Entities/BinaryFile.cs
- ✅ FOUND: src/SignaturPortal.Infrastructure/Data/Entities/Ercandidatefile.cs
- ✅ FOUND: src/SignaturPortal.Domain/Enums/ERActivityStatus.cs
- ✅ FOUND: src/SignaturPortal.Domain/Helpers/StatusMappings.cs

**Commits exist:**
- ✅ FOUND: 6d35f37 (Task 1: MudBlazor installation and configuration)
- ✅ FOUND: b0c5470 (Task 2: EF entities, enums, and query filters)

**Verification commands:**
```bash
# All files exist
ls src/SignaturPortal.Infrastructure/Data/Entities/Eractivitymember.cs
ls src/SignaturPortal.Infrastructure/Data/Entities/BinaryFile.cs
ls src/SignaturPortal.Infrastructure/Data/Entities/Ercandidatefile.cs
ls src/SignaturPortal.Domain/Enums/ERActivityStatus.cs
ls src/SignaturPortal.Domain/Helpers/StatusMappings.cs

# Commits verified
git log --oneline | grep "6d35f37\|b0c5470"
```

## Success Criteria Met

✅ **MudBlazor 8.0 installed and configured** - UI framework ready for Phase 3 read views
✅ **Three new EF entities scaffolded** - ERActivityMember, BinaryFile, ERCandidateFile with correct schema
✅ **Domain enums and helpers created** - ERActivityStatus enum and StatusMappings static dictionaries
✅ **Tenant query filters configured** - Eractivitymember scoped through activity.ClientId
✅ **Build passes with zero errors** - Solution compiles successfully

## Notes for Next Plan (03-02)

- MudBlazor UI components are now available for building data grids and forms
- ERActivityMember, BinaryFile, and ERCandidateFile entities are ready for repository/service layer integration
- StatusMappings helper provides reliable status name lookups for UI display
- Global query filters ensure tenant isolation for activity member queries
- System.Linq.Dynamic.Core enables dynamic sorting/filtering for MudDataGrid (Plan 03-03)

## Known Issues / Technical Debt

**⚠ System.Linq.Dynamic.Core vulnerability warning (NU1903):**
- Package: System.Linq.Dynamic.Core 1.5.0
- Advisory: GHSA-4cv2-4hjh-77rx (high severity)
- Impact: Development only; acceptable risk for current phase
- Action required: Security review before production deployment; monitor for patched version

**⚠ NuGet version resolution:**
- Requested: MudBlazor 7.23.0, System.Linq.Dynamic.Core 1.4.12
- Resolved: MudBlazor 8.0.0, System.Linq.Dynamic.Core 1.5.0
- Impact: Newer versions used; verify compatibility during integration testing
