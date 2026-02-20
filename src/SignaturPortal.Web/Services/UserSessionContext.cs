using SignaturPortal.Application.Interfaces;

namespace SignaturPortal.Web.Services;

public class UserSessionContext : IUserSessionContext
{
    private readonly ICurrentUserService _currentUserService;

    public UserSessionContext(ICurrentUserService currentUserService)
    {
        _currentUserService = currentUserService;
    }

    public Guid? UserId { get; private set; }
    public int? SiteId { get; private set; }
    public int? ClientId { get; private set; }
    public string UserName { get; private set; } = string.Empty;
    public string? FullName { get; private set; }
    public int UserLanguageId { get; private set; }
    public bool IsInitialized { get; private set; }
    public bool IsClientUser => ClientId.HasValue && ClientId.Value > 0;
    public bool IsImpersonating { get; private set; }
    public string? ImpersonatedByFullName { get; private set; }

    /// <summary>
    /// Initialize from the DB-backed user record for the given UserName.
    /// Called by UserSessionMiddleware during SSR HTTP requests (after UseAuthentication).
    /// Receives UserName from HttpContext.User.Identity.Name — avoids AuthenticationStateProvider
    /// which is only valid inside the Blazor circuit, not in the HTTP middleware pipeline.
    /// If impersonatedByUserId is provided, looks up the impersonating admin's FullName from DB.
    /// </summary>
    public async Task InitializeAsync(string? userName, Guid? impersonatedByUserId = null)
    {
        if (IsInitialized)
            return;

        var user = await _currentUserService.GetUserByNameAsync(userName ?? string.Empty);
        if (user is null)
            return;

        UserId = user.UserId;
        SiteId = user.SiteId > 0 ? user.SiteId : null;
        ClientId = user.ClientId.HasValue && user.ClientId.Value > 0 ? user.ClientId : null;
        UserName = user.UserName ?? string.Empty;
        FullName = user.FullName;
        UserLanguageId = user.UserLanguageId;

        if (impersonatedByUserId.HasValue)
        {
            var originalUser = await _currentUserService.GetUserByIdAsync(impersonatedByUserId.Value);
            ImpersonatedByFullName = originalUser?.FullName;
            IsImpersonating = true;
        }

        IsInitialized = true;
    }

    /// <summary>
    /// Restore from persisted values (used when SignalR circuit starts and
    /// HTTP context is no longer available).
    /// Called by SessionPersistence component via PersistentComponentState.
    /// </summary>
    public void Restore(Guid? userId, int? siteId, int? clientId, string userName, string? fullName, int userLanguageId,
        bool isImpersonating, string? impersonatedByFullName)
    {
        if (IsInitialized)
            return;

        UserId = userId;
        SiteId = siteId > 0 ? siteId : null;
        ClientId = clientId > 0 ? clientId : null;
        UserName = userName;
        FullName = fullName;
        UserLanguageId = userLanguageId;
        IsImpersonating = isImpersonating;
        ImpersonatedByFullName = impersonatedByFullName;
        IsInitialized = true;
    }
}
