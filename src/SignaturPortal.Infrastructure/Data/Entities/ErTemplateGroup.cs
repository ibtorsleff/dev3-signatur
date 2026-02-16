using System;

namespace SignaturPortal.Infrastructure.Data.Entities;

/// <summary>
/// Lookup entity for recruitment template groups.
/// Minimal mapping: only Id and Name needed for activity list display.
/// </summary>
public partial class ErTemplateGroup
{
    public int ErtemplateGroupId { get; set; }

    public string Name { get; set; } = "";
}
