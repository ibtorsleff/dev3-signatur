using System;
using System.Collections.Generic;

namespace SignaturPortal.Infrastructure.Data.Entities;

public partial class Client
{
    public int ClientId { get; set; }

    public int SiteId { get; set; }

    public string? ObjectData { get; set; }

    public string? ObjectDataHistory { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTime CreateDate { get; set; }

    public DateTime? ModifiedDate { get; set; }

    public virtual ICollection<Eractivity> Eractivities { get; set; } = new List<Eractivity>();

    public virtual Site Site { get; set; } = null!;
}
