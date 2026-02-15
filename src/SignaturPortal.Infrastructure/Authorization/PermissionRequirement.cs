using Microsoft.AspNetCore.Authorization;

namespace SignaturPortal.Infrastructure.Authorization;

/// <summary>
/// Authorization requirement that demands the user has a specific permission ID.
/// </summary>
public class PermissionRequirement : IAuthorizationRequirement
{
    public int PermissionId { get; }

    public PermissionRequirement(int permissionId)
    {
        PermissionId = permissionId;
    }
}
