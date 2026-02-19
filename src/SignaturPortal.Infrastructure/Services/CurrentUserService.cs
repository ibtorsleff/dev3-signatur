using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using SignaturPortal.Application.DTOs;
using SignaturPortal.Application.Interfaces;
using SignaturPortal.Infrastructure.Data;
using SignaturPortal.Infrastructure.Data.Entities;

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

        _cachedUser = await LoadUserAsync(userName, ct);
        _loaded = true;
        return _cachedUser;
    }

    public async Task<CurrentUserDto?> GetUserByNameAsync(string userName, CancellationToken ct = default)
    {
        return await LoadUserAsync(userName, ct);
    }

    private async Task<CurrentUserDto?> LoadUserAsync(string? userName, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(userName))
            return null;

        await using var db = await _contextFactory.CreateDbContextAsync(ct);

        var user = await db.Users
            .Where(u => u.UserName == userName)
            .FirstOrDefaultAsync(ct);

        if (user is null)
            return null;

        var userLanguageId = await GetUserLanguageIdAsync(db, user.UserName!, user.SiteId, ct);
        return new CurrentUserDto(
            UserId: user.UserId,
            FullName: user.FullName,
            UserName: user.UserName,
            Email: user.Email,
            IsInternal: user.IsInternal,
            Enabled: user.Enabled ?? false,
            SiteId: user.SiteId,
            ClientId: user.ClientId,
            UserLanguageId: userLanguageId);
    }

    /// <summary>
    /// Resolves the user's UI language ID from the database.
    /// Reads the Language abbreviation ("DK", "EN", etc.) from User.ObjectData XML.
    /// If absent or "Default", falls back to the site's default LanguageId.
    /// Matches legacy BasePage.UserLanguageId logic.
    /// </summary>
    private static async Task<int> GetUserLanguageIdAsync(
        SignaturDbContext db, string userName, int siteId, CancellationToken ct)
    {
        // Subquery extracts the language abbreviation from ObjectData XML and the SiteId
        // in one pass; the outer CASE then resolves it to a LanguageId.
        // COALESCE chain: user language → site default → 1 (EN, last-resort fallback).
        var result = await db.Database
            .SqlQueryRaw<int>(
                @"SELECT
                    COALESCE(
                        CASE WHEN sub.LangAbbr IS NULL OR sub.LangAbbr = 'Default'
                            THEN NULL
                            ELSE (SELECT TOP 1 l.LanguageId FROM Languages l
                                  WHERE l.Abbreviation = sub.LangAbbr)
                        END,
                        (SELECT TOP 1 s.LanguageId FROM Site s WHERE s.SiteId = sub.SiteId),
                        1
                    ) AS Value
                  FROM (
                      SELECT
                          ObjectData.value('(/AtlantaUser/Language)[1]', 'NVARCHAR(10)') AS LangAbbr,
                          SiteId
                      FROM [User]
                      WHERE UserName = {0}
                  ) AS sub",
                userName)
            .ToListAsync(ct);

        return result.FirstOrDefault();
    }
}
