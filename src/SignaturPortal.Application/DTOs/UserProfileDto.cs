namespace SignaturPortal.Application.DTOs;

/// <summary>
/// Profile field values and field configuration loaded for display and editing
/// in the user profile dialog. Mirrors legacy UserProfile.ascx data + SetupVarious().
/// </summary>
public class UserProfileDto
{
    public Guid UserId { get; init; }
    public string? WorkArea { get; init; }
    public string? Title { get; init; }
    public string? OfficePhone { get; init; }
    public string? CellPhone { get; init; }
    public string? Email { get; init; }
    public string? UserName { get; init; }
    public bool IsInternal { get; init; }
    public UserProfileConfigDto Config { get; init; } = new();
}
