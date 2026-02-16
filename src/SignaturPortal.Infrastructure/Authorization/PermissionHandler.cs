using Microsoft.AspNetCore.Authorization;
using SignaturPortal.Application.Interfaces;

namespace SignaturPortal.Infrastructure.Authorization;

/// <summary>
/// Evaluates PermissionRequirement by checking IPermissionService.
/// Uses IUserSessionContext (populated by UserSessionMiddleware before authorization)
/// to get the current user's identity — not claims, which may not contain a usable GUID.
/// </summary>
public class PermissionHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly IPermissionService _permissionService;
    private readonly IUserSessionContext _session;

    public PermissionHandler(IPermissionService permissionService, IUserSessionContext session)
    {
        _permissionService = permissionService;
        _session = session;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (!_session.IsInitialized || string.IsNullOrEmpty(_session.UserName))
            return; // requirement not satisfied → access denied

        if (await _permissionService.HasPermissionAsync(_session.UserName, requirement.PermissionId))
        {
            context.Succeed(requirement);
        }
    }
}
