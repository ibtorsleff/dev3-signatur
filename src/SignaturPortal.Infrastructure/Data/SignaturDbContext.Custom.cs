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
            entity.HasOne(d => d.Eractivity).WithMany(p => p.Ercandidates)
                .HasForeignKey(d => d.EractivityId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ERCandidate_ERActivity");
        });

        // ERActivityAlternativeResponsible — composite key join table (ERActivityId, UserId)
        modelBuilder.Entity<EractivityAlternativeResponsible>(entity =>
        {
            entity.ToTable("ERActivityAlternativeResponsible");
            entity.HasKey(e => new { e.EractivityId, e.UserId });
            entity.Property(e => e.EractivityId).HasColumnName("ERActivityId");

            entity.HasOne(d => d.Eractivity).WithMany()
                .HasForeignKey(d => d.EractivityId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(d => d.User).WithMany()
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull);
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

        // Eractivitymember: filter through Eractivity → ClientId
        modelBuilder.Entity<Eractivitymember>()
            .HasQueryFilter(m => CurrentClientId == null || m.Eractivity.ClientId == CurrentClientId);

        // AspnetRole: filter by SiteId + ClientId (null ClientId = site-wide role, include those)
        modelBuilder.Entity<AspnetRole>()
            .HasQueryFilter(r => CurrentSiteId == null ||
                (r.SiteId == CurrentSiteId && (r.ClientId == null || r.ClientId == CurrentClientId)));

        // [User] table: NO global query filter needed.
        // User data is accessed only through ERActivityMember joins (already tenant-filtered).
        // Adding a ClientId filter would break external hiring team member name resolution
        // (external members may belong to a different client).

        // --- Template / lookup table mappings ---

        modelBuilder.Entity<JobnetOccupation>(entity =>
        {
            entity.ToTable("JobnetOccupation");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(255);
        });

        modelBuilder.Entity<ErApplicationTemplate>(entity =>
        {
            entity.ToTable("ERApplicationTemplate");
            entity.HasKey(e => e.ErApplicationTemplateId);
            entity.Property(e => e.ErApplicationTemplateId).HasColumnName("ERApplicationTemplateId");
            entity.Property(e => e.Name).HasMaxLength(255);
        });

        modelBuilder.Entity<ErLetterTemplate>(entity =>
        {
            entity.ToTable("ERLetterTemplate");
            entity.HasKey(e => e.ErLetterTemplateId);
            entity.Property(e => e.ErLetterTemplateId).HasColumnName("ERLetterTemplateId");
            entity.Property(e => e.TemplateName).HasMaxLength(255);
        });

        modelBuilder.Entity<ErSmsTemplate>(entity =>
        {
            entity.ToTable("ERSmsTemplate");
            entity.HasKey(e => e.ErSmsTemplateId);
            entity.Property(e => e.ErSmsTemplateId).HasColumnName("ERSmsTemplateId");
            entity.Property(e => e.TemplateName).HasMaxLength(255);
        });

        modelBuilder.Entity<ErTemplateGroupApplicationTemplate>(entity =>
        {
            entity.ToTable("ERTemplateGroupERApplicationTemplate");
            entity.HasKey(e => new { e.ErTemplateGroupId, e.ErApplicationTemplateId });
            entity.Property(e => e.ErTemplateGroupId).HasColumnName("ERTemplateGroupId");
            entity.Property(e => e.ErApplicationTemplateId).HasColumnName("ERApplicationTemplateId");
        });
    }
}
