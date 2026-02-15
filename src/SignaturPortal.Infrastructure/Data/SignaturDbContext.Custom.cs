using Microsoft.EntityFrameworkCore;
using SignaturPortal.Infrastructure.Data.Entities;

namespace SignaturPortal.Infrastructure.Data;

public partial class SignaturDbContext
{
    /// <summary>
    /// Current site ID for multi-tenancy filtering.
    /// Set by UnitOfWork from IUserSessionContext. Null = no filtering (admin/system context).
    /// </summary>
    public int? CurrentSiteId { get; set; }

    /// <summary>
    /// Current client ID for multi-tenancy filtering.
    /// Set by UnitOfWork from IUserSessionContext. Null = no filtering (admin/system context).
    /// </summary>
    public int? CurrentClientId { get; set; }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
    {
        // Ercandidate → Eractivity navigation (needed for tenant filtering through nav property)
        modelBuilder.Entity<Ercandidate>(entity =>
        {
            entity.HasOne(d => d.Eractivity).WithMany()
                .HasForeignKey(d => d.EractivityId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ERCandidate_ERActivity");
        });

        // PermissionInRole join table
        modelBuilder.Entity<PermissionInRole>(entity =>
        {
            entity.ToTable("PermissionInRole");
            entity.HasKey(e => new { e.RoleId, e.PermissionId });

            entity.HasOne(d => d.Role).WithMany(r => r.PermissionInRoles)
                .HasForeignKey(d => d.RoleId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(d => d.Permission).WithMany()
                .HasForeignKey(d => d.PermissionId)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        // --- Multi-tenancy query filters ---

        // Client: filter by SiteId
        modelBuilder.Entity<Client>()
            .HasQueryFilter(c => CurrentSiteId == null || c.SiteId == CurrentSiteId);

        // Eractivity: filter by ClientId
        modelBuilder.Entity<Eractivity>()
            .HasQueryFilter(e => CurrentClientId == null || e.ClientId == CurrentClientId);

        // Ercandidate: filter through Eractivity → ClientId
        modelBuilder.Entity<Ercandidate>()
            .HasQueryFilter(ec => CurrentClientId == null || ec.Eractivity.ClientId == CurrentClientId);

        // AspnetRole: filter by SiteId + ClientId (null ClientId = site-wide role, include those)
        modelBuilder.Entity<AspnetRole>()
            .HasQueryFilter(r => CurrentSiteId == null ||
                (r.SiteId == CurrentSiteId && (r.ClientId == null || r.ClientId == CurrentClientId)));
    }
}
