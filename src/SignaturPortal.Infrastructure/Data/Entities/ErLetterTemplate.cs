namespace SignaturPortal.Infrastructure.Data.Entities;

public class ErLetterTemplate
{
    public int ErLetterTemplateId { get; set; }
    public int ClientId { get; set; }
    public string TemplateName { get; set; } = null!;
    public bool Active { get; set; }
    public int TypeId { get; set; }
}
