using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using SignaturPortal.Application.DTOs;
using SignaturPortal.Application.Interfaces;
using SignaturPortal.Infrastructure.Data;

namespace SignaturPortal.Infrastructure.Services;

/// <summary>
/// Provides DB-backed access to the current user's [User] table record.
/// Lazy-loads on first call and caches for the Blazor circuit lifetime (Scoped).
///
/// The Blazor-correct equivalent of HttpContext.User.Identity.Name is AuthenticationStateProvider —
/// it gives you the same ClaimsPrincipal from the auth cookie, but works throughout the circuit lifecycle.
/// </summary>
public class CurrentUserService : ICurrentUserService
{
    private readonly IDbContextFactory<SignaturDbContext> _contextFactory;
    private readonly AuthenticationStateProvider _authStateProvider;
    private CurrentUserDto? _cachedUser;
    private bool _loaded;

    public CurrentUserService(
        IDbContextFactory<SignaturDbContext> contextFactory,
        AuthenticationStateProvider authStateProvider)
    {
        _contextFactory = contextFactory;
        _authStateProvider = authStateProvider;
    }

    public async Task<CurrentUserDto?> GetCurrentUserAsync(CancellationToken ct = default)
    {
        if (_loaded)
            return _cachedUser;

        var authState = await _authStateProvider.GetAuthenticationStateAsync();
        var userName = authState.User.Identity?.Name;

        if (!string.IsNullOrEmpty(userName))
        {
            await using var db = await _contextFactory.CreateDbContextAsync(ct);

            var user = await db.Users
                .Where(u => u.UserName == userName)
                .FirstOrDefaultAsync(ct);

            if (user != null)
            {
                _cachedUser = new CurrentUserDto(
                    UserId: user.UserId,
                    FullName: user.FullName,
                    UserName: user.UserName,
                    Email: user.Email,
                    IsInternal: user.IsInternal,
                    Enabled: user.Enabled ?? false,
                    SiteId: user.SiteId,
                    ClientId: user.ClientId);
            }
        }

        _loaded = true;
        return _cachedUser;
    }
}
