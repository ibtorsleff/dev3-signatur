namespace SignaturPortal.Infrastructure.Data.Entities;

public class ErSmsTemplate
{
    public int ErSmsTemplateId { get; set; }
    public int ClientId { get; set; }
    public string TemplateName { get; set; } = null!;
    public bool Active { get; set; }
}
