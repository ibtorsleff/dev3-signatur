using System;

namespace SignaturPortal.Infrastructure.Data.Entities;

/// <summary>
/// Lookup entity for recruitment template groups.
/// </summary>
public partial class ErTemplateGroup
{
    public int ErtemplateGroupId { get; set; }

    public int ClientId { get; set; }

    public string Name { get; set; } = "";

    public bool Active { get; set; }
}
