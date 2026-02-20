namespace SignaturPortal.Application.DTOs;

/// <summary>
/// Flat DTO for activity list grid display.
/// Contains all fields needed for mode-aware activity list columns.
/// Name fields are resolved via SQL JOINs to avoid N+1 queries.
/// </summary>
public record ActivityListDto
{
    public int EractivityId { get; init; }
    public string Headline { get; init; } = "";
    public string Jobtitle { get; init; } = "";
    public DateTime ApplicationDeadline { get; init; }
    public bool ContinuousPosting { get; init; }
    public int EractivityStatusId { get; init; }
    public DateTime CreateDate { get; init; }
    public int CandidateCount { get; init; }

    // Web ad visitor count (from WebAdVisitors table via LEFT JOIN on WebAdId)
    public int WebAdVisitors { get; init; }

    // Resolved display names (populated via SQL JOINs, no N+1)
    public string RecruitingResponsibleName { get; init; } = "";
    public string CreatedByName { get; init; } = "";
    public string DraftResponsibleName { get; init; } = "";
    public string ClientSectionName { get; init; } = "";
    public string TemplateGroupName { get; init; } = "";

    // Draft Area name: populated only in Draft mode.
    // When AreaType==ERTemplateGroup: same as TemplateGroupName.
    // When AreaType==ClientSection: top-level parent section name via CTE hierarchy traversal.
    public string DraftAreaName { get; init; } = "";

    // Active candidate count: IsDeleted=false AND status NOT Hired(3)/Rejected(4).
    // Used as the tooltip denominator on the ID cell, matching legacy CandidateTotalCount.
    public int ActiveCandidateCount { get; init; }

    // Row styling fields — used to compute color class and cleaned-row style.
    // Populated in GetActivitiesAsync; zero/false when not applicable (Draft/Closed mode, user not a member).
    public bool IsUserMember { get; init; }
    public bool CandidateEvaluationEnabled { get; init; }
    public int CandidateMissingEvaluationCount { get; init; }
    public int CandidateNotReadCount { get; init; }
    public bool IsCleaned { get; init; }

    // Icon 1: email warning (non-client users, EditActivitiesNotMemberOf permission, OnGoing only)
    public int MembersMissingNotificationEmail { get; init; }

    // Icon 2: web ad status (client users, client ShowWebAdStatus config, OnGoing only)
    public bool HasWebAdMedia { get; init; }
    public bool HasJobnetMedia { get; init; }
    public int? WebAdId { get; init; }
    public int? JobnetWebAdId { get; init; }
    public int? WebAdStatusId { get; init; }
    public int? JobnetStatusId { get; init; }

    // Icon 3: web ad changes (client users, WebAdSendMailOnAdChanges config + PublishWebAd permission)
    public bool HasWebAdChanges { get; init; }
    public string? WebAdChangeSummary { get; init; }
}
