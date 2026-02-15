using SignaturPortal.Application.Interfaces;

namespace SignaturPortal.Web.Services;

public class UserSessionContext : IUserSessionContext
{
    public int UserId { get; private set; }
    public int SiteId { get; private set; }
    public int ClientId { get; private set; }
    public string UserName { get; private set; } = string.Empty;
    public int UserLanguageId { get; private set; }
    public bool IsInitialized { get; private set; }

    public void Initialize()
    {
        if (IsInitialized)
            return;

        var swSession = System.Web.HttpContext.Current?.Session;
        if (swSession is null)
            return;

        UserId = swSession["UserId"] is int uid ? uid : 0;
        SiteId = swSession["SiteId"] is int sid ? sid : 0;
        ClientId = swSession["ClientId"] is int cid ? cid : 0;
        UserName = swSession["UserName"] as string ?? string.Empty;
        UserLanguageId = swSession["UserLanguageId"] is int lid ? lid : 0;

        IsInitialized = true;
    }
}
