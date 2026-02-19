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
    /// Resolves the UserName from AuthenticationStateProvider (Blazor circuit context).
    /// Returns null if UserName is empty or user not found in DB.
    /// Cached for the Blazor circuit lifetime (Scoped DI). Thread safety: not needed in Blazor Server.
    /// </summary>
    Task<CurrentUserDto?> GetCurrentUserAsync(CancellationToken ct = default);

    /// <summary>
    /// Loads a user record directly by UserName — for use outside the Blazor circuit
    /// (e.g., HTTP middleware) where AuthenticationStateProvider is not available.
    /// The caller supplies the UserName from HttpContext.User.Identity.Name.
    /// Not cached — always queries the DB.
    /// </summary>
    Task<CurrentUserDto?> GetUserByNameAsync(string userName, CancellationToken ct = default);
}
