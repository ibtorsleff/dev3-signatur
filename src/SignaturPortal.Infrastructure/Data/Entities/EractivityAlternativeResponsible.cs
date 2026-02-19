namespace SignaturPortal.Infrastructure.Data.Entities;

public class EractivityAlternativeResponsible
{
    public int EractivityId { get; set; }

    public Guid UserId { get; set; }

    public virtual Eractivity Eractivity { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
