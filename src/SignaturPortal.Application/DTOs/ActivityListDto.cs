namespace SignaturPortal.Application.DTOs;

/// <summary>
/// Flat DTO for activity list grid display.
/// Contains all fields needed for the activity list view without nested objects.
/// </summary>
public record ActivityListDto
{
    public int EractivityId { get; init; }
    public string Headline { get; init; } = "";
    public string Jobtitle { get; init; } = "";
    public string JournalNo { get; init; } = "";
    public DateTime ApplicationDeadline { get; init; }
    public int EractivityStatusId { get; init; }
    public string StatusName { get; init; } = "";
    public DateTime CreateDate { get; init; }
    public int CandidateCount { get; init; }
    public Guid? Responsible { get; init; }
    public Guid? CreatedBy { get; init; }
}
