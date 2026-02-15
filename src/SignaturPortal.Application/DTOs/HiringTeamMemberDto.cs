namespace SignaturPortal.Application.DTOs;

/// <summary>
/// Hiring team member DTO with user details and permissions.
/// </summary>
public record HiringTeamMemberDto
{
    public int EractivityMemberId { get; init; }
    public Guid UserId { get; init; }
    public string UserName { get; init; } = "";
    public string FullName { get; init; } = "";
    public string Email { get; init; } = "";
    public int MemberTypeId { get; init; }
    public string MemberTypeName { get; init; } = "";
    public bool? AllowCandidateManagement { get; init; }
    public bool? AllowCandidateReview { get; init; }
    public bool? AllowViewEditNotes { get; init; }
    public bool? NotificationMailSendToUser { get; init; }
}
