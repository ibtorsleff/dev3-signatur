using Microsoft.EntityFrameworkCore;
using SignaturPortal.Application.Interfaces;
using SignaturPortal.Infrastructure.Data;

namespace SignaturPortal.Infrastructure.Services;

/// <summary>
/// Queries the user's permissions via the same join path as legacy:
/// aspnet_UsersInRoles → aspnet_Roles (active + tenant-scoped via query filter) → PermissionInRole → Permission.
/// Results are cached per-request (scoped lifetime).
/// </summary>
public class PermissionService : IPermissionService
{
    private readonly IDbContextFactory<SignaturDbContext> _contextFactory;
    private readonly IUserSessionContext _session;
    private IReadOnlySet<int>? _cachedPermissions;
    private Guid? _cachedUserId;

    public PermissionService(IDbContextFactory<SignaturDbContext> contextFactory, IUserSessionContext session)
    {
        _contextFactory = contextFactory;
        _session = session;
    }

    public async Task<bool> HasPermissionAsync(Guid userId, int permissionId, CancellationToken ct = default)
    {
        var permissions = await GetUserPermissionsAsync(userId, ct);
        return permissions.Contains(permissionId);
    }

    public async Task<IReadOnlySet<int>> GetUserPermissionsAsync(Guid userId, CancellationToken ct = default)
    {
        // Return cached if same user within this scope
        if (_cachedPermissions is not null && _cachedUserId == userId)
            return _cachedPermissions;

        await using var db = await _contextFactory.CreateDbContextAsync(ct);

        // Stamp tenant so query filters scope roles to current tenant
        if (_session.IsInitialized)
        {
            db.CurrentSiteId = _session.SiteId;
            db.CurrentClientId = _session.ClientId;
        }

        var permissionIds = await db.AspnetUsers
            .Where(u => u.UserId == userId)
            .SelectMany(u => u.Roles) // through aspnet_UsersInRoles join
            .Where(r => r.IsActive)   // only active roles (query filter also applies tenant scoping)
            .SelectMany(r => r.PermissionInRoles)
            .Select(pir => pir.PermissionId)
            .Distinct()
            .ToListAsync(ct);

        _cachedPermissions = new HashSet<int>(permissionIds);
        _cachedUserId = userId;
        return _cachedPermissions;
    }
}
