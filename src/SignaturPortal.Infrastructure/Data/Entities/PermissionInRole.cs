namespace SignaturPortal.Infrastructure.Data.Entities;

public class PermissionInRole
{
    public Guid RoleId { get; set; }

    public int PermissionId { get; set; }

    public virtual AspnetRole Role { get; set; } = null!;

    public virtual Permission Permission { get; set; } = null!;
}
