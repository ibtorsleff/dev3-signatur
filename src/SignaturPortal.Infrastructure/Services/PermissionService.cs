using Microsoft.EntityFrameworkCore;
using SignaturPortal.Application.Interfaces;
using SignaturPortal.Infrastructure.Data;

namespace SignaturPortal.Infrastructure.Services;

/// <summary>
/// Queries the user's permissions via the same join path as legacy:
/// aspnet_Users (by UserName) → aspnet_UsersInRoles → aspnet_Roles (active + tenant-scoped) → PermissionInRole.
/// Results are cached per-request (scoped lifetime).
/// </summary>
public class PermissionService : IPermissionService
{
    private readonly IDbContextFactory<SignaturDbContext> _contextFactory;
    private readonly IUserSessionContext _session;
    private IReadOnlySet<int>? _cachedPermissions;
    private string? _cachedUserName;

    public PermissionService(IDbContextFactory<SignaturDbContext> contextFactory, IUserSessionContext session)
    {
        _contextFactory = contextFactory;
        _session = session;
    }

    public async Task<bool> HasPermissionAsync(string userName, int permissionId, CancellationToken ct = default)
    {
        var permissions = await GetUserPermissionsAsync(userName, ct);
        return permissions.Contains(permissionId);
    }

    public async Task<IReadOnlySet<int>> GetUserPermissionsAsync(string userName, CancellationToken ct = default)
    {
        // Return cached if same user within this scope
        if (_cachedPermissions is not null && string.Equals(_cachedUserName, userName, StringComparison.OrdinalIgnoreCase))
            return _cachedPermissions;

        await using var db = await _contextFactory.CreateDbContextAsync(ct);

        // Stamp tenant so query filters scope roles to current tenant
        if (_session.IsInitialized)
        {
            db.CurrentSiteId = _session.SiteId;
            db.CurrentClientId = _session.ClientId;
        }

        var loweredName = userName.ToLowerInvariant();

        var permissionIds = await db.AspnetUsers
            .Where(u => u.LoweredUserName == loweredName)
            .SelectMany(u => u.Roles) // through aspnet_UsersInRoles join
            .Where(r => r.IsActive)   // only active roles (query filter also applies tenant scoping)
            .SelectMany(r => r.PermissionInRoles)
            .Select(pir => pir.PermissionId)
            .Distinct()
            .ToListAsync(ct);

        _cachedPermissions = new HashSet<int>(permissionIds);
        _cachedUserName = userName;
        return _cachedPermissions;
    }
}
