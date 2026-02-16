namespace SignaturPortal.Application.Interfaces;

public interface IPermissionService
{
    Task<bool> HasPermissionAsync(string userName, int permissionId, CancellationToken ct = default);
    Task<IReadOnlySet<int>> GetUserPermissionsAsync(string userName, CancellationToken ct = default);
}
