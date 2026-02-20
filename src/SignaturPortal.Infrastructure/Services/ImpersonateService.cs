using Microsoft.EntityFrameworkCore;
using SignaturPortal.Application.Authorization;
using SignaturPortal.Application.DTOs;
using SignaturPortal.Application.Interfaces;
using SignaturPortal.Infrastructure.Data;

namespace SignaturPortal.Infrastructure.Services;

/// <summary>
/// Searches users available for impersonation.
/// Uses raw SQL against [User] to avoid loading ObjectData XML into C# for ClientSection.
/// Mirrors legacy AtlantaUserHelper.UsersForImpersonateGet / UsersForImpersonateCount.
/// </summary>
public class ImpersonateService : IImpersonateService
{
    private readonly IDbContextFactory<SignaturDbContext> _contextFactory;
    private readonly IUserSessionContext _session;
    private readonly IPermissionService _permissionService;

    public ImpersonateService(
        IDbContextFactory<SignaturDbContext> contextFactory,
        IUserSessionContext session,
        IPermissionService permissionService)
    {
        _contextFactory = contextFactory;
        _session = session;
        _permissionService = permissionService;
    }

    public async Task<IReadOnlyList<ImpersonateUserDto>> SearchUsersAsync(
        string searchText,
        int? clientId,
        CancellationToken ct = default)
    {
        var siteId = _session.SiteId ?? 0;
        var currentUserId = _session.UserId ?? Guid.Empty;
        var searchPattern = $"%{searchText}%";

        // Callers without AdPortalCreateEditSignaturUsers cannot see users who hold that permission.
        // Mirrors legacy: Impersonate.ascx.cs PopulateUserList exclusion list {9020, 9022}.
        // We exclude only 9020 here, matching the plan spec.
        bool callerCanManageSignaturUsers = await _permissionService.HasPermissionAsync(
            _session.UserName, (int)PortalPermission.AdPortalCreateEditSignaturUsers, ct);

        // Build parameters list and conditional SQL clauses dynamically.
        // Index 0 = siteId, 1 = currentUserId, 2 = searchPattern; clientId appended if provided.
        var parameters = new List<object> { siteId, currentUserId, searchPattern };

        string clientFilter = string.Empty;
        if (clientId.HasValue)
        {
            clientFilter = $"AND u.ClientId = {{{parameters.Count}}}";
            parameters.Add(clientId.Value);
        }

        string permissionFilter = callerCanManageSignaturUsers
            ? string.Empty
            : "AND u.UserId NOT IN (SELECT UserId FROM UserPermission WHERE PermissionId = 9020)";

        var sql = $@"SELECT TOP 51
    u.UserId,
    u.FullName,
    u.Title,
    u.Email,
    u.ObjectData.value('(/AtlantaUser/ClientSection)[1]', 'NVARCHAR(200)') AS ClientSection,
    c.ObjectData.value('(/Client/ClientName)[1]', 'NVARCHAR(128)') AS ClientName
FROM [User] u
LEFT JOIN Client c ON c.ClientId = u.ClientId
WHERE u.SiteId = {{0}}
  AND u.Enabled = 1
  AND u.UserId != {{1}}
  AND (u.FullName LIKE {{2}} OR u.Email LIKE {{2}} OR u.UserName LIKE {{2}})
  {clientFilter}
  {permissionFilter}
ORDER BY u.FullName";

        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        return await context.Database
            .SqlQueryRaw<ImpersonateUserDto>(sql, parameters.ToArray())
            .ToListAsync(ct);
    }
}
