namespace SignaturPortal.Application.DTOs;

/// <summary>
/// Candidate detail DTO with full contact information and attached files.
/// Used for candidate detail page display.
/// </summary>
public record CandidateDetailDto
{
    public int ErcandidateId { get; init; }
    public int EractivityId { get; init; }
    public string FirstName { get; init; } = "";
    public string LastName { get; init; } = "";
    public string FullName => $"{FirstName} {LastName}".Trim();
    public string Email { get; init; } = "";
    public string Telephone { get; init; } = "";
    public string Address { get; init; } = "";
    public string City { get; init; } = "";
    public string ZipCode { get; init; } = "";
    public DateTime RegistrationDate { get; init; }
    public DateOnly? DateOfBirth { get; init; }
    public int ErcandidateStatusId { get; init; }
    public string StatusName { get; init; } = "";
    public bool IsDeleted { get; init; }
    public bool IsInternalCandidate { get; init; }
    public int LanguageId { get; init; }
    public List<CandidateFileDto> Files { get; init; } = new();
    public string ActivityHeadline { get; init; } = "";
}
