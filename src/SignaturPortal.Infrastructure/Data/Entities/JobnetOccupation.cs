namespace SignaturPortal.Infrastructure.Data.Entities;

public class JobnetOccupation
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public bool Deleted { get; set; }
    public int? Level { get; set; }
}
