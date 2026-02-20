# ActivityList Row Styling — Implementation Plan

## CSS Class Naming Convention

Classes are named by **what they mean**, not by color number.

| Legacy class | New class | Color | Meaning |
|---|---|---|---|
| `color-1` | `activity-row-needs-review` | `#fadfaa` (orange) | Candidates waiting for review, deadline not yet exceeded |
| `color-2` | `activity-row-overdue` | `#fac0c0` (red) | Candidates waiting for review AND deadline has passed |
| `color-3` | `activity-row-reviewed` | `#e8e8e8` (gray) | All candidates reviewed, activity still active |
| `list-item-row-closed` | `activity-row-cleaned` | — | Activity is cleaned/archived (gray italic text) |

---

## Option A — Row Colors (implement now)

### Business Rules

Color is only applied when **all** of the following hold:
- `EractivityStatusId == OnGoing`
- `IsUserMember == true` (current user is a member of this activity)

Unread candidate check:
- If `CandidateEvaluationEnabled == true` → unread when `CandidateMissingEvaluationCount > 0`
- If `CandidateEvaluationEnabled == false` → unread when `CandidateNotReadCount > 0`

Deadline exceeded check:
- `ContinuousPosting == false AND ApplicationDeadline < today`

Color decision tree:
```
HasUnread == true
  → deadline NOT exceeded → activity-row-needs-review  (#fadfaa orange)
  → deadline exceeded     → activity-row-overdue        (#fac0c0 red)
HasUnread == false
  → deadline NOT exceeded → activity-row-reviewed       (#e8e8e8 gray)
  → deadline exceeded     → (no class — color-4 had no CSS in legacy either)
```

IsCleaned override (independent of color):
- `IsCleaned == true` → add `activity-row-cleaned` to the `<tr>` → gray italic text on all cells

### Files to Change

#### 1. `ActivityListDto.cs`
Add properties:
```csharp
public bool IsUserMember { get; set; }
public bool CandidateEvaluationEnabled { get; set; }
public int CandidateMissingEvaluationCount { get; set; }
public int CandidateNotReadCount { get; set; }
public bool IsCleaned { get; set; }
```

#### 2. Query / Repository
File: `IActivityRepository` implementation (Infrastructure layer).

Extend the `ActivityListDto` projection with:
- `IsUserMember` — LEFT JOIN `ERActivityMember` ON `ERActivityId = activity.EractivityId AND UserId = @currentUserId`, result IS NOT NULL
- `CandidateEvaluationEnabled` — from `ERActivity.CandidateEvaluationEnabled`
- `CandidateMissingEvaluationCount` — subquery: COUNT of candidates where `EvaluationStatusId IS NULL` or not yet evaluated, scoped to the current user's ActivityMember
- `CandidateNotReadCount` — subquery: COUNT of unread candidates for the current user's ActivityMember
- `IsCleaned` — from `ERActivity.IsCleaned` (or equivalent column name in DB)

> **Note:** Verify exact column/table names against the DB before writing SQL. The legacy queries in `ActivityList.ascx.cs` are the reference.

#### 3. `ActivityList.razor.cs`
Add helper method:
```csharp
private string GetActivityRowColorClass(ActivityListDto item)
{
    if (item.EractivityStatusId != (int)ERActivityStatus.OnGoing) return string.Empty;
    if (!item.IsUserMember) return string.Empty;

    bool hasUnread = item.CandidateEvaluationEnabled
        ? item.CandidateMissingEvaluationCount > 0
        : item.CandidateNotReadCount > 0;

    bool deadlineExceeded = !item.ContinuousPosting
        && item.ApplicationDeadline < DateTime.Today;

    if (hasUnread)
        return deadlineExceeded ? "activity-row-overdue" : "activity-row-needs-review";

    return deadlineExceeded ? string.Empty : "activity-row-reviewed";
}

private string GetActivityRowClass(ActivityListDto item)
    => item.IsCleaned ? "activity-row-cleaned" : string.Empty;
```

#### 4. `ActivityList.razor`
Replace fixed `RowStyle` with `RowClassFunc` and apply per-cell color on the ID column.

MudDataGrid does not support per-cell class functions directly, so apply the color as a **left border stripe** on the row, or apply it to all cells via `RowClassFunc` targeting a CSS rule scoped to the first `<td>`.

Approach — apply color class to `<tr>` and use CSS `:first-child` to color only the ID cell:
```razor
<MudDataGrid ...
    RowClassFunc="@(item => $"{GetActivityRowClass(item)} {GetActivityRowColorClass(item)}")"
    RowStyle="cursor: pointer;">
```

Then scope the CSS so the color only appears on the first `<td>`:
```css
/* Only color the ID cell (first column), matching legacy behavior */
.mud-table-row.activity-row-needs-review td:first-child { background-color: #fadfaa; }
.mud-table-row.activity-row-overdue td:first-child       { background-color: #fac0c0; }
.mud-table-row.activity-row-reviewed td:first-child      { background-color: #e8e8e8; }

/* Cleaned row — affects all cells */
.mud-table-row.activity-row-cleaned td {
    color: #b0a0a0 !important;
    font-style: italic !important;
}
```

> **Alternative:** If the `:first-child` CSS approach proves unreliable with MudBlazor's rendered DOM, switch to a `CellTemplate` on the ID column that injects an inline `style` based on the color class.

#### 5. CSS Location
Add to the portal's shared stylesheet (not a scoped `.razor.css` — MudBlazor CSS isolation does not work):
- File: `SignaturPortal.Web/wwwroot/css/portal.css` (or wherever global portal styles live)

---

## Option B — Action Column Icons (next session)

### Icons to Implement

Three icon slots to the right of the existing copy icon, shown for OnGoing activities only.

#### Icon 1 — Email Warning
- **Image:** `/Responsive/images/responsive/list/email-warning-gray_36x32.png`
- **Shown when:** `EractivityStatusId == OnGoing AND MembersMissingNotificationEmail > 0 AND userHasPermissionEditActivitiesNotMemberOf`
- **Tooltip:** localized string with count (singular/plural variants)
- **Hidden for:** client users, limited-access users

**DTO property to add:** `int MembersMissingNotificationEmail`
**Query:** subquery COUNT on `ERActivityMember` where notification email not sent

#### Icon 2 — Web Ad Status (7 sub-rules, evaluated in order)
- **Image (missing):** `/Responsive/images/responsive/list/web-ad-missing_36x36.png`
- **Image (draft):** `/Responsive/images/responsive/list/web-ad-draft_36x36.png`
- **Shown when:** `_showWebAdStatus == true AND EractivityStatusId == OnGoing`
- **Clickable:** navigates to the activity's Ad tab

Sub-rules (first match wins):
1. No `WebAdId` (≤ 0) → missing icon, tooltip `NoWebAd`
2. Has WebAd media + no WebAdId + has Jobnet media → missing icon, tooltip `NoWebAdAndJobnetAd`
3. Has WebAd media + no WebAdId + no Jobnet media → missing icon, tooltip `NoWebAd`
4. Has Jobnet media + no `JobnetWebAdId` → missing icon, tooltip `NoJobnetAd`
5. Both WebAd + Jobnet in Draft → draft icon, tooltip `WebAdAndJobnetAdInDraftStatus`
6. Only WebAd in Draft → draft icon, tooltip `WebAdInDraftStatus`
7. Only Jobnet in Draft → draft icon, tooltip `JobnetAdInDraftStatus`

**DTO properties to add:**
```csharp
public bool HasWebAdMedia { get; set; }
public bool HasJobnetMedia { get; set; }
public int? WebAdId { get; set; }
public int? JobnetWebAdId { get; set; }
public int? WebAdStatusId { get; set; }    // map to WebAdStatus enum
public int? JobnetStatusId { get; set; }   // map to JobnetStatus enum
```

**Query:** LEFT JOINs to WebAd and Jobnet publication tables.
**Prerequisite:** Verify table/column names against legacy `ERActivity`, `ERWebAd`, and Jobnet tables.

#### Icon 3 — Web Ad Changes
- **Image:** `/Responsive/images/responsive/list/insertion-is-new_28x36.png`
- **Shown when:** `_isClientUser == true AND HasWebAdChanges == true`
- **Tooltip:** summary of changed fields (headline, deadline, etc.)

**DTO properties to add:**
```csharp
public bool HasWebAdChanges { get; set; }
public string? WebAdChangeSummary { get; set; }  // pre-built tooltip string from query/service
```

**Query:** check for pending change records in the WebAdChange/delta table.

### Implementation Notes for Option B

- Determine whether `_showWebAdStatus` is a per-client config flag or a global setting; expose it as a component field loaded in `OnInitializedAsync`.
- The email warning column visibility guard (`_tmpAnyRowMissingNotificationEmail`) means: only show the column header slot at all if at least one row has a missing email. In Blazor, this is simpler — just conditionally render the icon per-row; the column header can remain hidden if no rows show the icon.
- Icon 2 is clickable and navigates to the activity's Ad tab — use `NavigationManager.NavigateTo` with the correct route.
- Web ad change tooltip is a multi-line HTML string in the legacy. In MudBlazor use `MudTooltip` with `RootStyle` to allow HTML, or use a `MudPopover` if rich content is needed.
