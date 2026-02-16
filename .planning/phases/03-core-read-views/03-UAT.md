---
status: testing
phase: 03-core-read-views
source: [03-01-SUMMARY.md, 03-02-SUMMARY.md, 03-03-SUMMARY.md, 03-04-SUMMARY.md, 03-05-SUMMARY.md, 03-06-SUMMARY.md]
started: 2026-02-15T23:30:00Z
updated: 2026-02-15T23:35:00Z
---

## Current Test

[testing paused - diagnosing blocker]

## Tests

### 1. Activity List Page Loads with Data
expected: Navigate to /activities. A MudDataGrid appears showing recruitment activities with columns: Activity (headline), Journal No, Deadline, Status, Candidates, Created. The teal theme (#1a9b89) is applied. Data rows are visible in the grid.
result: issue
reported: "empty list on load - No recruitment activities found."
severity: major

### 2. Activity List Pagination
expected: At the bottom of the activity grid, a pager shows page size options (10, 25, 50, 100). Changing page size reloads data with the correct number of rows. Page navigation (next/previous) works.
result: issue
reported: "non responsive"
severity: major

### 3. Activity List Sorting
expected: Click a column header (e.g. "Deadline"). Data re-sorts by that column. Click again to reverse. A sort indicator arrow appears on the sorted column.
result: skipped
reason: user requested skip - blocked by test 1

### 4. Activity Row Click Navigation
expected: Click any row in the activity list. Browser navigates to /activities/{id} showing that activity's detail page.
result: skipped
reason: blocked by test 1 - no rows to click

### 5. NavMenu Links to Activities
expected: In the top navigation, the "Sagsliste" (Row 1) and "Rekruttering" (Row 2) links navigate to /activities.
result: [pending]

### 6. Activity Detail Page Layout
expected: On /activities/{id}, see a header card with: Headline (large text), status chip (color-coded: green=Ongoing, grey=Closed, red=Deleted, yellow=Draft), and a details table showing Job Title, Journal No, Deadline, Hire Date, Created, Responsible name, Created By name.
result: skipped
reason: blocked by test 1

### 7. Hiring Team Display
expected: On the activity detail page, a "Hiring Team" card shows a table with columns: Name, Email, Type (chip: Internal/External), Review, Manage, Notes, Notifications (checkmarks/crosses for permissions). If no members, shows "No hiring team members assigned."
result: skipped
reason: blocked by test 1

### 8. Candidates Summary on Detail
expected: Below the hiring team, a "Candidates" card shows the candidate count and a "View Candidates" button. Clicking the button navigates to /activities/{id}/candidates.
result: skipped
reason: blocked by test 1

### 9. Breadcrumb Navigation
expected: On the activity detail page, breadcrumbs show "Activities > {Headline}". Clicking "Activities" navigates back to the list.
result: skipped
reason: blocked by test 1

### 10. Activity Not Found (Error Handling)
expected: Navigate to /activities/999999 (non-existent ID). A warning alert appears saying "Activity not found" (or similar). The page remains interactive -- no circuit death.
result: [pending]

### 11. Candidate List Page
expected: Navigate to /activities/{id}/candidates. A search field appears at the top. Below it, a MudDataGrid shows candidates with columns: Name, Email, Phone, City, Applied (date), Status, Files (count).
result: skipped
reason: blocked by test 1

### 12. Candidate Name Search
expected: Type a name (or partial) in the search field on the candidate list. After a brief pause (~300ms), the grid reloads showing only matching candidates. Clear the search to see all again.
result: skipped
reason: blocked by test 1

### 13. Candidate Detail Page
expected: Click a candidate in the list. Navigate to /activities/{id}/candidates/{candidateId}. See candidate contact info (email, phone, address, city, DOB, applied date) and an attachments section listing files with name, size, and download button.
result: skipped
reason: blocked by test 1

### 14. File Download
expected: On the candidate detail page, click a download button next to a file. A loading spinner appears on the button. The browser triggers a file download with the correct filename. Success snackbar appears.
result: skipped
reason: blocked by test 1

## Summary

total: 14
passed: 0
issues: 2
pending: 2
skipped: 10

## Gaps

- truth: "Activity list page loads and displays recruitment activities in the MudDataGrid"
  status: failed
  reason: "User reported: empty list on load - No recruitment activities found."
  severity: major
  test: 1
  root_cause: ""
  artifacts: []
  missing: []
  debug_session: ""

- truth: "Activity list pagination controls are functional"
  status: failed
  reason: "User reported: non responsive"
  severity: major
  test: 2
  root_cause: ""
  artifacts: []
  missing: []
  debug_session: ""
