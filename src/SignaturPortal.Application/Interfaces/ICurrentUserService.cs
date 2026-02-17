using SignaturPortal.Application.DTOs;

namespace SignaturPortal.Application.Interfaces;

/// <summary>
/// Provides access to the currently logged-in user's DB record from the [User] table.
/// Lookup is keyed by UserName from the authenticated identity — the auth cookie is the source of truth.
/// Scoped to the Blazor circuit lifetime — data is loaded once per circuit and cached.
/// </summary>
public interface ICurrentUserService
{
    /// <summary>
    /// Lazy-loads the current user's [User] table record from the database by UserName.
    /// Returns null if session is not initialized, UserName is empty, or user not found in DB.
    /// Cached for the Blazor circuit lifetime (Scoped DI). Thread safety: not needed in Blazor Server.
    /// </summary>
    Task<CurrentUserDto?> GetCurrentUserAsync(CancellationToken ct = default);
}
