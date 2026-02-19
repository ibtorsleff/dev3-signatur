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
}
