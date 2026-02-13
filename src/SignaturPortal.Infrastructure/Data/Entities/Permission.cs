using System;
using System.Collections.Generic;

namespace SignaturPortal.Infrastructure.Data.Entities;

public partial class Permission
{
    public int PermissionId { get; set; }

    public string PermissionName { get; set; } = null!;

    public string? Description { get; set; }

    public int PermissionGroupId { get; set; }

    public int PermissionTypeId { get; set; }

    public int SortOrder { get; set; }

    public string TextKey { get; set; } = null!;

    public string InfoTextKey { get; set; } = null!;
}
