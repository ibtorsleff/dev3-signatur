namespace SignaturPortal.Infrastructure.Data.Entities;

/// <summary>
/// Lookup entity for client section groups.
/// Groups are optional — only used when ClientSectionGroupsEnabled for the client.
/// </summary>
public partial class ClientSectionGroup
{
    public int ClientSectionGroupId { get; set; }

    public int ClientId { get; set; }

    public string Name { get; set; } = "";
}
