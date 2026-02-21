using SignaturPortal.Application.DTOs;

namespace SignaturPortal.Application.Interfaces;

/// <summary>
/// Service for searching users available for impersonation.
/// Mirrors legacy AtlantaUserHelper.UsersForImpersonateGet / UsersForImpersonateCount.
/// </summary>
public interface IImpersonateService
{
    /// <summary>
    /// Searches users eligible for impersonation within the current site.
    /// Returns up to 51 rows — callers should check Count > 50 and call
    /// <see cref="CountUsersAsync"/> to obtain the real total for display.
    ///
    /// Exclusions applied:
    ///   - Current user is always excluded.
    ///   - When the caller lacks AdPortalCreateEditSignaturUsers (9020), users who hold that
    ///     permission are excluded (mirrors legacy behaviour).
    ///   - Only enabled users are returned.
    /// </summary>
    Task<IReadOnlyList<ImpersonateUserDto>> SearchUsersAsync(
        string searchText,
        int? clientId,
        CancellationToken ct = default);

    /// <summary>
    /// Returns the total number of users that match the same filters as
    /// <see cref="SearchUsersAsync"/> without a row limit.
    /// Call this when SearchUsersAsync returns 51 rows to get the real count for display.
    /// Mirrors legacy AtlantaUserHelper.UsersForImpersonateCount.
    /// </summary>
    Task<int> CountUsersAsync(
        string searchText,
        int? clientId,
        CancellationToken ct = default);
}
