using Microsoft.EntityFrameworkCore;
using SignaturPortal.Application.Interfaces;
using SignaturPortal.Infrastructure.Data;

namespace SignaturPortal.Web.Services;

public class UserSessionContext(IDbContextFactory<SignaturDbContext> contextFactory) : IUserSessionContext
{
    public Guid? UserId { get; private set; }
    public int? SiteId { get; private set; }
    public int? ClientId { get; private set; }
    public string UserName { get; private set; } = string.Empty;
    public int UserLanguageId { get; private set; }
    public bool IsInternal { get; private set; }
    public bool IsInitialized { get; private set; }
    public bool IsClientUser => ClientId.HasValue && ClientId.Value > 0;

    /// <summary>
    /// Initialize from legacy System.Web session (only works during SSR HTTP requests).
    /// Queries IsInternal from Blazor's own DB using UserId — does not rely on legacy session.
    /// Called by UserSessionMiddleware.
    /// </summary>
    public async Task InitializeAsync()
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

        if (UserId.HasValue)
        {
            await using var db = await contextFactory.CreateDbContextAsync();
            IsInternal = await db.Users
                .Where(u => u.UserId == UserId.Value)
                .Select(u => u.IsInternal)
                .FirstOrDefaultAsync();
        }

        IsInitialized = true;
    }

    /// <summary>
    /// Restore from persisted values (used when SignalR circuit starts and
    /// System.Web session is no longer available).
    /// Called by SessionPersistence component via PersistentComponentState.
    /// </summary>
    public void Restore(Guid? userId, int? siteId, int? clientId, string userName, int userLanguageId, bool isInternal)
    {
        if (IsInitialized)
            return;

        UserId = userId;
        SiteId = siteId > 0 ? siteId : null;
        ClientId = clientId > 0 ? clientId : null;
        UserName = userName;
        UserLanguageId = userLanguageId;
        IsInternal = isInternal;
        IsInitialized = true;
    }
}
