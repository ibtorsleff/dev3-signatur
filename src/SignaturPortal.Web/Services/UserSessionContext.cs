using SignaturPortal.Application.Interfaces;

namespace SignaturPortal.Web.Services;

public class UserSessionContext : IUserSessionContext
{
    public Guid? UserId { get; private set; }
    public int? SiteId { get; private set; }
    public int? ClientId { get; private set; }
    public string UserName { get; private set; } = string.Empty;
    public int UserLanguageId { get; private set; }
    public bool IsInitialized { get; private set; }
    public bool IsClientUser => ClientId.HasValue && ClientId.Value > 0;

    /// <summary>
    /// Initialize from legacy System.Web session (only works during SSR HTTP requests).
    /// Called by UserSessionMiddleware.
    /// </summary>
    public void Initialize()
    {
        if (IsInitialized)
            return;

        var swSession = System.Web.HttpContext.Current?.Session;
        if (swSession is null)
            return;

        UserId = swSession["UserId"] is Guid uid ? uid : null;
        SiteId = swSession["SiteId"] is int sid && sid > 0 ? sid : null;
        ClientId = swSession["ClientId"] is int cid && cid > 0 ? cid : null;
        UserName = swSession["UserName"] as string ?? string.Empty;
        UserLanguageId = swSession["UserLanguageId"] is int lid ? lid : 0;

        IsInitialized = true;
    }

    /// <summary>
    /// Restore from persisted values (used when SignalR circuit starts and
    /// System.Web session is no longer available).
    /// Called by SessionPersistence component via PersistentComponentState.
    /// </summary>
    public void Restore(Guid? userId, int? siteId, int? clientId, string userName, int userLanguageId)
    {
        if (IsInitialized)
            return;

        UserId = userId;
        SiteId = siteId > 0 ? siteId : null;
        ClientId = clientId > 0 ? clientId : null;
        UserName = userName;
        UserLanguageId = userLanguageId;
        IsInitialized = true;
    }
}
