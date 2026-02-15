using System;
using System.Collections.Generic;

namespace SignaturPortal.Infrastructure.Data.Entities;

public partial class AspnetRole
{
    public Guid ApplicationId { get; set; }

    public Guid RoleId { get; set; }

    public string RoleName { get; set; } = null!;

    public string LoweredRoleName { get; set; } = null!;

    public string? Description { get; set; }

    public int SiteId { get; set; }

    public int? ClientId { get; set; }

    public bool IsActive { get; set; }

    public bool IsSystemRole { get; set; }

    public bool IsSpecialRole { get; set; }

    public bool ClientCanUse { get; set; }

    public bool IsDefaultOnboardingRole { get; set; }

    public virtual ICollection<AspnetUser> Users { get; set; } = new List<AspnetUser>();

    public virtual ICollection<PermissionInRole> PermissionInRoles { get; set; } = new List<PermissionInRole>();
}
