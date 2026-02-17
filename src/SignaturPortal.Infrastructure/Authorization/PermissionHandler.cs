using Microsoft.AspNetCore.Authorization;
using SignaturPortal.Application.Interfaces;

namespace SignaturPortal.Infrastructure.Authorization;

/// <summary>
/// Evaluates PermissionRequirement by checking IPermissionService.
/// Uses context.User.Identity.Name from the System.Web Adapters authentication — available
/// reliably during both SSR and SPA navigation, unlike IUserSessionContext which depends on
/// the session being loaded before authorization runs (timing-sensitive).
/// </summary>
public class PermissionHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly IPermissionService _permissionService;

    public PermissionHandler(IPermissionService permissionService)
    {
        _permissionService = permissionService;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        var userName = context.User.Identity?.Name;
        if (string.IsNullOrEmpty(userName))
            return; // not authenticated → access denied

        if (await _permissionService.HasPermissionAsync(userName, requirement.PermissionId))
        {
            context.Succeed(requirement);
        }
    }
}
