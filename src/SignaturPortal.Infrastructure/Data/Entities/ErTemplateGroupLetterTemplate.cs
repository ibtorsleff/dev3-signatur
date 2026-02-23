namespace SignaturPortal.Infrastructure.Data.Entities;

/// <summary>
/// Join table entity for ERTemplateGroupERLetterTemplate.
/// Maps template groups to email (letter) templates (many-to-many).
/// </summary>
public class ErTemplateGroupLetterTemplate
{
    public int ErTemplateGroupId { get; set; }
    public int ErLetterTemplateId { get; set; }
}
