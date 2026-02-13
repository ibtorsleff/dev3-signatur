using System;
using System.Collections.Generic;

namespace SignaturPortal.Infrastructure.Data.Entities;

public partial class AspnetUser
{
    public Guid ApplicationId { get; set; }

    public Guid UserId { get; set; }

    public string UserName { get; set; } = null!;

    public string LoweredUserName { get; set; } = null!;

    public string? MobileAlias { get; set; }

    public bool IsAnonymous { get; set; }

    public DateTime LastActivityDate { get; set; }

    public virtual AspnetMembership? AspnetMembership { get; set; }

    public virtual ICollection<AspnetRole> Roles { get; set; } = new List<AspnetRole>();
}
