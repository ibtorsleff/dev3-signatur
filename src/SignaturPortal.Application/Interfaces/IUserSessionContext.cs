namespace SignaturPortal.Application.Interfaces;

public interface IUserSessionContext
{
    Guid? UserId { get; }
    int? SiteId { get; }
    int? ClientId { get; }
    string UserName { get; }
    string? FullName { get; }
    int UserLanguageId { get; }
    bool IsInitialized { get; }

    /// <summary>
    /// True when the user belongs to a client organization (ClientId > 0).
    /// Matches legacy PermissionHelper.UserIsClient.
    /// </summary>
    bool IsClientUser { get; }
}
