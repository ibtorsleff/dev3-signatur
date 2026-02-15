---
phase: 03-core-read-views
plan: 03
subsystem: recruitment-activities
tags: [read-view, detail-page, hiring-team, user-lookup, clean-architecture]
dependency-graph:
  requires: [03-02, User-table, StatusMappings]
  provides: [ActivityDetailDto, HiringTeamMemberDto, GetActivityDetailAsync, /activities/{id}]
  affects: [IActivityService, ActivityService, SignaturDbContext]
tech-stack:
  added: [User entity, hiring team navigation]
  patterns: [DTO projection, split query, in-memory status mapping, user lookup join]
key-files:
  created:
    - src/SignaturPortal.Infrastructure/Data/Entities/User.cs
    - src/SignaturPortal.Application/DTOs/HiringTeamMemberDto.cs
    - src/SignaturPortal.Web/Components/Pages/Activities/ActivityDetail.razor
    - src/SignaturPortal.Web/Components/Pages/Activities/ActivityDetail.razor.cs
  modified:
    - src/SignaturPortal.Infrastructure/Data/Entities/Eractivitymember.cs
    - src/SignaturPortal.Infrastructure/Data/SignaturDbContext.cs
    - src/SignaturPortal.Infrastructure/Data/SignaturDbContext.Custom.cs
    - src/SignaturPortal.Application/DTOs/ActivityDetailDto.cs
    - src/SignaturPortal.Application/Interfaces/IActivityService.cs
    - src/SignaturPortal.Infrastructure/Services/ActivityService.cs
decisions:
  - decision: No tenant filter on User table
    rationale: User data accessed only through ERActivityMember joins (already tenant-filtered); adding filter would break external hiring team member name resolution
    documented: SignaturDbContext.Custom.cs comment
  - decision: Use AsSplitQuery for activity detail query
    rationale: Avoids cartesian explosion when loading both hiring team members and candidate count
    impact: Two SQL queries instead of one, but better performance
  - decision: Resolve StatusMappings and member type names in-memory
    rationale: StatusMappings dictionary cannot be translated to SQL
    pattern: Project entity IDs in query, map to names after materialization
metrics:
  duration: 5 minutes
  tasks-completed: 2/2
  files-created: 4
  files-modified: 6
  commits: 2
  completed-date: 2026-02-15
---

# Phase 03 Plan 03: Activity Detail View with Hiring Team Summary

**One-liner:** Activity detail page showing all properties, hiring team with User table joins for names/emails, candidate count, with tenant-scoped access.

## What Was Built

### Task 1: Scaffold User table and extend ActivityService
**Commit:** 218f4dc

Scaffolded the [User] table from the database to enable name/email resolution for hiring team members and activity responsible/created by users.

**User Entity:**
- Contains: UserId (GUID), SiteId, ClientId, FullName, UserName, Email, Phone, Title, etc.
- Added DbSet<User> to SignaturDbContext
- Updated Eractivitymember navigation to point to User (not AspnetUser)
- **Tenant filtering decision:** No global query filter applied to User table (documented with comment)

**Rationale for no tenant filter:**
- User data is never queried directly by end users
- Always accessed through ERActivityMember joins (already tenant-filtered via activity.ClientId)
- External hiring team members (MemberTypeId=2,3) may belong to different clients
- Adding a ClientId filter would break cross-client user lookups

**DTOs:**
- Completed ActivityDetailDto with 28 properties: activity fields, responsible/created by names, hiring team members, candidate count
- Created HiringTeamMemberDto with user details (FullName, Email, UserName) and permissions

**Service Layer:**
- Extended IActivityService with GetActivityDetailAsync(activityId, ct)
- Implemented in ActivityService with:
  - Tenant-scoped query via global filters (CurrentSiteId, CurrentClientId)
  - Single activity query with AsSplitQuery to avoid cartesian explosion
  - Join to User table for Responsible/CreatedBy name resolution
  - Hiring team members loaded with User.FullName, User.Email navigation
  - Member type names resolved via StatusMappings.GetActivityMemberTypeName (in-memory)
  - Status names resolved via StatusMappings.GetActivityStatusName (in-memory)
  - Candidate count as subquery (Count of non-deleted candidates)
  - Returns null if activity not found or cross-tenant access attempt

**Query Pattern:**
```csharp
// Single query with split to avoid cartesian explosion
var activity = await context.Eractivities
    .Where(a => a.EractivityId == activityId && !a.IsCleaned)
    .Select(a => new {
        // Activity properties
        a.Headline, a.Jobtitle, /* ... */
        // Candidate count
        CandidateCount = a.Ercandidates.Count(c => !c.IsDeleted),
        // Hiring team members with User join
        HiringTeamMembers = a.Eractivitymembers.Select(m => new {
            m.UserId, m.User.FullName, m.User.Email, /* ... */
        }).ToList()
    })
    .AsSplitQuery()
    .FirstOrDefaultAsync(ct);

// Separate query for responsible/created by names
var userLookup = await context.Users
    .Where(u => userIds.Contains(u.UserId))
    .ToDictionaryAsync(u => u.UserId, u => u.FullName ?? u.UserName ?? "", ct);
```

### Task 2: Build Activity Detail page
**Commit:** 9e2f087

Created the ActivityDetail.razor page at route `/activities/{id}` with full activity information display.

**Page Features:**
- Breadcrumb navigation: Activities > Detail (headline)
- Loading state with progress spinner
- Not-found state with warning alert (cross-tenant or non-existent activity)
- Three-card layout: Activity Header, Hiring Team, Candidates Summary

**Activity Header Card:**
- Headline with color-coded status chip
- Two-column layout (core details left, flags/counts right)
- Core details: Job Title, Journal Number, Application Deadline, Hire Date, Created Date, Status Changed, Responsible, Created By
- Flags/counts: Continuous Posting, Candidate Evaluation, Email on New Candidate, Is Cleaned, Total Candidates, Hiring Team Members, Client ID, Activity ID
- Status chip colors: Success=Ongoing, Default=Closed, Error=Deleted, Warning=Draft

**Hiring Team Card:**
- Table with 7 columns: Name, Email, Type, Review, Manage, Notes, Notifications
- Member type chips (color-coded): Primary=Internal, Secondary=External, Warning=External Draft
- Permission flags: Checkmark (✓) for true, Cross (✗) for false, Dash (-) for null
- Empty state: "No hiring team members assigned to this activity."

**Candidates Summary Card:**
- Candidate count chip
- Text summary: "This activity has X candidate(s) associated with it."
- Button link to `/activities/{id}/candidates` (view candidates page)

**UI Components:**
- MudCard, MudCardHeader, MudCardContent for layout
- MudSimpleTable with Dense/Hover/Striped/Bordered styling
- MudChip for status, member type, and counts
- MudBreadcrumbs for navigation
- MudButton for candidate list link
- MudAlert for error states
- MudProgressCircular for loading

**Code-Behind Pattern:**
- ActivityDetail.razor.cs with OnInitializedAsync lifecycle
- Calls IActivityService.GetActivityDetailAsync
- Updates breadcrumb with activity headline after load
- Helper methods: GetStatusColor, GetMemberTypeColor, BoolDisplay

## Deviations from Plan

None - plan executed exactly as written.

## Verification Results

**Build Status:**
- `dotnet build SignaturPortal.slnx` compiles with 0 errors
- 11 nullable warnings (expected in Razor - null checks in template)

**Artifacts Verified:**
- [User] table scaffolded: ✓
- User entity has FullName, Email, UserName: ✓
- Eractivitymember navigation points to User: ✓
- SignaturDbContext.Custom.cs has tenant filtering comment: ✓
- ActivityDetailDto has all 28 properties: ✓
- HiringTeamMemberDto created with 11 properties: ✓
- IActivityService has GetActivityDetailAsync: ✓
- ActivityService implements GetActivityDetailAsync: ✓
- ActivityDetail.razor route is /activities/{id}: ✓
- Page has breadcrumb, loading, not-found, detail states: ✓
- Hiring team table has 7 columns: ✓
- Candidate summary links to /activities/{id}/candidates: ✓

**Clean Architecture Compliance:**
- Web layer references only IActivityService (Application interface): ✓
- No EF Core types in Web project: ✓
- DTOs in Application layer: ✓
- Service implementation in Infrastructure layer: ✓

**Tenant Isolation:**
- Global query filters applied to Eractivity query: ✓
- User table correctly excluded from tenant filtering: ✓
- Cross-tenant activity access returns null (not found): ✓

## Key Decisions

### 1. No Tenant Filter on User Table
**Context:** The [User] table has both SiteId and ClientId columns, suggesting it might need tenant filtering.

**Decision:** Do NOT add global query filter to User table.

**Rationale:**
- User is a lookup/reference table for name resolution
- Never queried directly by end users
- Always accessed through ERActivityMember joins (already tenant-filtered)
- External hiring team members (MemberTypeId=2,3) may belong to different client
- Adding ClientId filter would break cross-client user name lookups

**Documentation:** Added comment in SignaturDbContext.Custom.cs explaining rationale.

**Impact:** External collaborators from other tenants will have their names displayed correctly in hiring team lists.

### 2. AsSplitQuery for Activity Detail
**Context:** Query loads both hiring team members and candidate count from activity.

**Decision:** Use AsSplitQuery() on the activity detail query.

**Rationale:**
- Prevents cartesian explosion (members × candidates)
- Two separate SQL queries instead of one large join
- Better performance for activities with many members and candidates

**Impact:** Slightly more database round-trips, but dramatically better performance for large datasets.

### 3. In-Memory Status Mapping
**Context:** StatusMappings.GetActivityStatusName and GetActivityMemberTypeName are static dictionary lookups.

**Decision:** Resolve status/member type names after EF Core query materialization.

**Rationale:**
- StatusMappings dictionary cannot be translated to SQL
- Project entity status IDs in query, map to names in-memory

**Pattern Applied:**
```csharp
// In query: project IDs
StatusName = "", // Will be filled in-memory
MemberTypeName = ""

// After materialization: map to names
StatusName = StatusMappings.GetActivityStatusName(activity.EractivityStatusId)
MemberTypeName = StatusMappings.GetActivityMemberTypeName(member.MemberTypeId)
```

**Impact:** Consistent with Plan 02 approach; minimal performance impact (dictionary lookup is O(1)).

## What's Next

**Immediate:** Plan 03-04 already exists (Candidate List and Detail views) - appears to be partially executed based on commit history.

**Phase 3 Remaining:**
- 03-04: Candidate list and detail pages (status: appears executed)

**Blockers:** None.

**Dependencies Provided:**
- ActivityDetailDto available for other views
- HiringTeamMemberDto pattern for other team/member displays
- User table scaffolded for name resolution across app
- GetActivityDetailAsync for detail navigation from other pages

## Self-Check: PASSED

**Files Created:**
- src/SignaturPortal.Infrastructure/Data/Entities/User.cs: ✓ EXISTS
- src/SignaturPortal.Application/DTOs/HiringTeamMemberDto.cs: ✓ EXISTS
- src/SignaturPortal.Web/Components/Pages/Activities/ActivityDetail.razor: ✓ EXISTS
- src/SignaturPortal.Web/Components/Pages/Activities/ActivityDetail.razor.cs: ✓ EXISTS

**Files Modified:**
- src/SignaturPortal.Infrastructure/Data/Entities/Eractivitymember.cs: ✓ EXISTS
- src/SignaturPortal.Infrastructure/Data/SignaturDbContext.cs: ✓ EXISTS (DbSet<User> added)
- src/SignaturPortal.Infrastructure/Data/SignaturDbContext.Custom.cs: ✓ EXISTS (comment added)
- src/SignaturPortal.Application/DTOs/ActivityDetailDto.cs: ✓ EXISTS (completed)
- src/SignaturPortal.Application/Interfaces/IActivityService.cs: ✓ EXISTS (method added)
- src/SignaturPortal.Infrastructure/Services/ActivityService.cs: ✓ EXISTS (method implemented)

**Commits:**
- 218f4dc (Task 1): ✓ FOUND
- 9e2f087 (Task 2): ✓ FOUND

All artifacts verified. No missing files or commits.
