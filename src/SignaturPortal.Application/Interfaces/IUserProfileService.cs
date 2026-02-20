using SignaturPortal.Application.DTOs;

namespace SignaturPortal.Application.Interfaces;

/// <summary>
/// Manages user profile loading and saving for the Blazor profile dialog.
/// Migrated from legacy UserProfile.ascx.cs — preserves all business rules for
/// field visibility, required status, email editing, save sequencing, and
/// activity logging.
/// </summary>
public interface IUserProfileService
{
    /// <summary>
    /// Loads profile field values and field configuration (required, disabled, domain lists)
    /// for the given user. Returns null when the user record is not found.
    /// </summary>
    Task<UserProfileDto?> GetProfileAsync(Guid userId, int siteId, CancellationToken ct = default);

    /// <summary>
    /// Saves updated profile fields.
    /// Handles email + username changes, aspnet_Membership sync, activity logging.
    /// Returns a result indicating success and whether a forced re-login is required
    /// (UsernameUpdated = true when an email-based username was changed).
    /// </summary>
    Task<UserProfileUpdateResult> UpdateProfileAsync(UpdateUserProfileCommand command, CancellationToken ct = default);
}
