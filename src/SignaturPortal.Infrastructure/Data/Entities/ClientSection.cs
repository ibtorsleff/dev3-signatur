using System;

namespace SignaturPortal.Infrastructure.Data.Entities;

/// <summary>
/// Lookup entity for client organizational sections.
/// Minimal mapping: only Id and Name needed for activity list display.
/// </summary>
public partial class ClientSection
{
    public int ClientSectionId { get; set; }

    public string Name { get; set; } = "";

    public int? ClientSectionGroupId { get; set; }

    public virtual ClientSectionGroup? ClientSectionGroup { get; set; }
}
