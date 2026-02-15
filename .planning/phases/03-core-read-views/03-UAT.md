---
status: diagnosed
phase: 03-core-read-views
source: [03-01-SUMMARY.md, 03-02-SUMMARY.md, 03-03-SUMMARY.md, 03-04-SUMMARY.md]
started: 2026-02-15T23:00:00Z
updated: 2026-02-15T23:15:00Z
---

## Current Test

## Current Test

[testing complete]

## Tests

### 1. Activity List Page Loads
expected: Navigate to /activities. A MudDataGrid appears showing recruitment activities with columns: Activity (headline), Journal No, Deadline, Status, Candidates, Created. The page title says "Recruitment Activities". The teal theme (#1a9b89) is applied to the UI.
result: issue
reported: "the page is shown but no data is loaded"
severity: major

### 2. Activity List Pagination
expected: At the bottom of the activity grid, a pager appears with page size options (10, 25, 50, 100). Changing page size reloads data with the correct number of rows. Clicking next/previous page loads the next/previous set.
result: skipped
reason: no data available - cannot even change the "rows per page"

### 3. Activity List Sorting
expected: Click a column header (e.g., "Deadline"). Data re-sorts by that column. Click again to reverse sort order. A sort indicator arrow appears on the sorted column.
result: skipped
reason: no data loaded so cannot test now

### 4. Activity Row Click Navigation
expected: Click any row in the activity list. Browser navigates to /activities/{id} showing that activity's detail page. Alternatively, click the eye icon on the right side of a row for the same result.
result: skipped
reason: no data loaded - no rows to click

### 5. NavMenu Links to Activities
expected: In the top navigation, the "Sagsliste" (Row 1) and "Rekruttering" (Row 2) links navigate to /activities.
result: pass

### 6. Activity Detail Page
expected: On /activities/{id}, see an activity header card with: Headline (large text), status chip (color-coded: green=Ongoing, grey=Closed, red=Deleted, yellow=Draft), and a two-column table showing Job Title, Journal No, Deadline, Hire Date, Created, Responsible name, Created By name.
result: skipped
reason: no data loaded - cannot navigate to a real activity

### 7. Hiring Team Display
expected: On the activity detail page, below the header card, a "Hiring Team" card appears with a table showing columns: Name, Email, Type (chip: Internal/External), Review, Manage, Notes, Notifications (checkmarks/crosses for permissions). If no members, shows "No hiring team members assigned."
result: skipped
reason: no data loaded - cannot navigate to a real activity

### 8. Candidates Summary on Detail
expected: Below the hiring team card, a "Candidates" card shows the candidate count and a "View Candidates" button. Clicking the button navigates to /activities/{id}/candidates.
result: skipped
reason: no data loaded - cannot navigate to a real activity

### 9. Breadcrumb Navigation
expected: On the activity detail page, breadcrumbs show "Activities > {Headline}". Clicking "Activities" navigates back to the activity list. On candidate pages, breadcrumbs extend to show the full path.
result: skipped
reason: no data loaded - cannot navigate to a real activity

### 10. Activity Not Found
expected: Navigate to /activities/999999 (a non-existent ID). An alert/warning appears saying "Activity not found" instead of an error page.
result: issue
reported: "the page cleared and became unresponsive"
severity: blocker

### 11. Candidate List Page
expected: Navigate to /activities/{id}/candidates. A search field appears at the top. Below it, a MudDataGrid shows candidates with columns: Name, Email, Phone, City, Applied (date), Status, Files (count). Pager at bottom.
result: skipped
reason: no data loaded - data access broken (blocked by test 1)

### 12. Candidate Name Search
expected: Type a name (or partial name) in the search field on the candidate list. After a brief pause (~300ms), the grid reloads showing only candidates matching the search term. Clear the search to see all candidates again.
result: skipped
reason: no data loaded - blocked by test 1

### 13. Candidate Detail Page
expected: Click a candidate in the list. Navigate to /activities/{id}/candidates/{candidateId}. See candidate contact info (email, phone, address, city, DOB, applied date) and an attachments section listing files with file name, size (formatted as KB/MB), and a download button for each file.
result: skipped
reason: no data loaded - blocked by test 1

### 14. File Download
expected: On the candidate detail page, click a download button next to a file. A loading spinner appears on the button. The browser triggers a file save dialog (or downloads to default folder) with the correct filename. Success snackbar appears.
result: skipped
reason: no data loaded - blocked by test 1

## Summary

total: 14
passed: 1
issues: 2
pending: 0
skipped: 11

## Gaps

- truth: "Activity list page loads and displays recruitment activities in the MudDataGrid"
  status: failed
  reason: "User reported: the page is shown but no data is loaded"
  severity: major
  test: 1
  root_cause: "UserSessionContext uses int (defaults to 0) instead of int? (defaults to null) for ClientId/SiteId. Global query filter checks CurrentClientId == null for bypass, but gets 0 instead, filtering for ClientId=0 which matches no activities. SessionPersistence component may also fail to restore session values in the circuit."
  artifacts:
    - path: "src/SignaturPortal.Web/Services/UserSessionContext.cs"
      issue: "ClientId and SiteId are int (default 0) instead of int? (default null)"
    - path: "src/SignaturPortal.Infrastructure/Data/SignaturDbContext.Custom.cs"
      issue: "Query filter expects null for no filtering but receives 0"
    - path: "src/SignaturPortal.Web/Components/Layout/SessionPersistence.razor"
      issue: "Session restoration may be broken in circuit"
  missing:
    - "Change IUserSessionContext to use int? for UserId, SiteId, ClientId"
    - "Fix SessionPersistence to properly restore session values"
    - "Update ActivityService to handle nullable tenant context"
  debug_session: ".planning/debug/resolved/activity-list-no-data.md"

- truth: "Navigating to a non-existent activity ID shows 'Activity not found' alert gracefully"
  status: failed
  reason: "User reported: the page cleared and became unresponsive"
  severity: blocker
  test: 10
  root_cause: "Missing try-catch in ActivityDetail.razor.cs OnInitializedAsync. Any exception during GetActivityDetailAsync kills the Blazor Server circuit. The service has multiple exception points (DbContext creation, query execution, navigation property access) that are unprotected."
  artifacts:
    - path: "src/SignaturPortal.Web/Components/Pages/Activities/ActivityDetail.razor.cs"
      issue: "OnInitializedAsync has no try-catch around service call (line 27)"
    - path: "src/SignaturPortal.Infrastructure/Services/ActivityService.cs"
      issue: "GetActivityDetailAsync has multiple potential exception points"
  missing:
    - "Add try-catch in OnInitializedAsync, set _notFound=true on exception"
    - "Apply same pattern to all Blazor component lifecycle methods calling services"
  debug_session: ".planning/debug/activity-999999-circuit-crash.md"
