using Microsoft.EntityFrameworkCore;

namespace SignaturPortal.Infrastructure.Data;

public partial class SignaturDbContext
{
    /// <summary>
    /// Current site ID for multi-tenancy filtering.
    /// Set by TenantDbContextFactory or manually before queries.
    /// </summary>
    public string? CurrentSiteId { get; set; }

    /// <summary>
    /// Current client ID for multi-tenancy filtering.
    /// Set by TenantDbContextFactory or manually before queries.
    /// </summary>
    public int? CurrentClientId { get; set; }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
    {
        // Multi-tenancy query filters will be added in Phase 2 (SEC-02).
        // Placeholder for now to establish the partial class pattern.

        // Example of what Phase 2 will add:
        // modelBuilder.Entity<Client>()
        //     .HasQueryFilter(c => CurrentSiteId == null || c.SiteId == CurrentSiteId);
    }
}
