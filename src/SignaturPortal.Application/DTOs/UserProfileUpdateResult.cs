namespace SignaturPortal.Application.DTOs;

/// <summary>
/// Result of a profile update operation.
/// When UsernameUpdated is true the caller must force a re-login because the
/// authentication cookie still holds the old username (email-based username changed).
/// Mirrors the post-save logic in legacy UserProfile.ascx.cs SaveButton_OnClick.
/// </summary>
public class UserProfileUpdateResult
{
    public bool Success { get; private init; }
    public bool UsernameUpdated { get; private init; }
    public string NewUserName { get; private init; } = string.Empty;
    public bool NotificationMailSent { get; private init; }
    public string? ErrorMessage { get; private init; }

    public static UserProfileUpdateResult Saved() =>
        new() { Success = true };

    public static UserProfileUpdateResult SavedWithUsernameChange(string newUserName) =>
        new() { Success = true, UsernameUpdated = true, NewUserName = newUserName };

    public static UserProfileUpdateResult Failed(string error) =>
        new() { Success = false, ErrorMessage = error };
}
