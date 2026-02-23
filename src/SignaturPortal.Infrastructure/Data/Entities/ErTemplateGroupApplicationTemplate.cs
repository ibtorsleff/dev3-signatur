namespace SignaturPortal.Infrastructure.Data.Entities;

/// <summary>
/// Join table entity for ERTemplateGroupERApplicationTemplate.
/// Maps template groups to application templates (many-to-many).
/// </summary>
public class ErTemplateGroupApplicationTemplate
{
    public int ErTemplateGroupId { get; set; }
    public int ErApplicationTemplateId { get; set; }
}
