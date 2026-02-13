using System;
using System.Collections.Generic;

namespace SignaturPortal.Infrastructure.Data.Entities;

public partial class Site
{
    public int SiteId { get; set; }

    public bool? Enabled { get; set; }

    public string SiteName { get; set; } = null!;

    public string SiteUrls { get; set; } = null!;

    public string? PrimarySiteUrl { get; set; }

    public string? Description { get; set; }

    public DateTime? CreateDate { get; set; }

    public int LanguageId { get; set; }

    public int? ImagesTopLevelFolderId { get; set; }

    public int? TemplatesTopLevelFolderId { get; set; }

    public string? PlugInDll { get; set; }

    public string ExternalSiteId { get; set; } = null!;

    public string? ObjectData { get; set; }

    public virtual ICollection<Client> Clients { get; set; } = new List<Client>();
}
