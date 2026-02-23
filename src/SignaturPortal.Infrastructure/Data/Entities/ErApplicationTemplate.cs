namespace SignaturPortal.Infrastructure.Data.Entities;

public class ErApplicationTemplate
{
    public int ErApplicationTemplateId { get; set; }
    public int ClientId { get; set; }
    public string Name { get; set; } = null!;
    public bool Active { get; set; }
    public int ErApplicationTemplateTypeId { get; set; }
}
