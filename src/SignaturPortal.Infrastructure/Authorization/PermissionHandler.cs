using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using SignaturPortal.Application.Interfaces;

namespace SignaturPortal.Infrastructure.Authorization;

/// <summary>
/// Evaluates PermissionRequirement by checking IPermissionService.
/// The user's identity must contain a NameIdentifier claim with their GUID.
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
        var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userIdClaim is null || !Guid.TryParse(userIdClaim, out var userId))
            return; // fail open → requirement not satisfied → access denied

        if (await _permissionService.HasPermissionAsync(userId, requirement.PermissionId))
        {
            context.Succeed(requirement);
        }
    }
}
