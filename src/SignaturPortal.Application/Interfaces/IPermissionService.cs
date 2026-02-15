namespace SignaturPortal.Application.Interfaces;

public interface IPermissionService
{
    Task<bool> HasPermissionAsync(Guid userId, int permissionId, CancellationToken ct = default);
    Task<IReadOnlySet<int>> GetUserPermissionsAsync(Guid userId, CancellationToken ct = default);
}
