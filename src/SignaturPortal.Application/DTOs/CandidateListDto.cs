namespace SignaturPortal.Application.DTOs;

/// <summary>
/// Candidate list DTO for candidate grid display.
/// TODO: Complete in Plan 03-04 with all grid fields.
/// </summary>
public record CandidateListDto
{
    public int ErcandidateId { get; init; }
    public string FirstName { get; init; } = "";
    public string LastName { get; init; } = "";
    // TODO: Add remaining candidate fields in Plan 03-04
}
