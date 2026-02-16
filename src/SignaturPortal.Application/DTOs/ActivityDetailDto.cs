namespace SignaturPortal.Application.DTOs;

/// <summary>
/// Activity detail DTO for detail view with full activity properties,
/// hiring team members, and candidate count.
/// </summary>
public record ActivityDetailDto
{
    public int EractivityId { get; init; }
    public int ClientId { get; init; }
    public string Headline { get; init; } = "";
    public string Jobtitle { get; init; } = "";
    public string JournalNo { get; init; } = "";
    public int EractivityStatusId { get; init; }
    public string StatusName { get; init; } = "";
    public DateTime ApplicationDeadline { get; init; }
    public DateTime? HireDate { get; init; }
    public string? HireDateFreeText { get; init; }
    public DateTime CreateDate { get; init; }
    public DateTime StatusChangedTimeStamp { get; init; }
    public bool ContinuousPosting { get; init; }
    public bool CandidateEvaluationEnabled { get; init; }
    public bool IsCleaned { get; init; }
    public bool EmailOnNewCandidate { get; init; }
    public Guid? Responsible { get; init; }
    public string ResponsibleName { get; init; } = "";
    public Guid? CreatedBy { get; init; }
    public string CreatedByName { get; init; } = "";
    public int CandidateCount { get; init; }
    public int HiringTeamMemberCount { get; init; }
    public List<HiringTeamMemberDto> HiringTeamMembers { get; init; } = new();
}
