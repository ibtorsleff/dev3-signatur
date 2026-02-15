namespace SignaturPortal.Application.Interfaces;

public interface IUserSessionContext
{
    int? UserId { get; }
    int? SiteId { get; }
    int? ClientId { get; }
    string UserName { get; }
    int UserLanguageId { get; }
    bool IsInitialized { get; }
}
