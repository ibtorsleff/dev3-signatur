using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SignaturPortal.Infrastructure.Data;

namespace SignaturPortal.Infrastructure.Interceptors;

/// <summary>
/// EF Core interceptor that rejects writes where an entity's ClientId
/// does not match the DbContext's CurrentClientId. Prevents cross-tenant data leaks.
/// </summary>
public class TenantSaveChangesInterceptor : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData, InterceptionResult<int> result)
    {
        ValidateTenant(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ValidateTenant(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private static void ValidateTenant(DbContext? context)
    {
        if (context is not SignaturDbContext db)
            return;

        // Skip validation when no tenant context is set (admin/system scenarios)
        if (db.CurrentClientId is null)
            return;

        var entries = db.ChangeTracker.Entries()
            .Where(e => e.State is EntityState.Added or EntityState.Modified);

        foreach (var entry in entries)
        {
            var clientIdProp = entry.Properties
                .FirstOrDefault(p => p.Metadata.Name == "ClientId");

            if (clientIdProp is null)
                continue;

            var entityClientId = clientIdProp.CurrentValue;
            if (entityClientId is int id && id != db.CurrentClientId)
            {
                throw new InvalidOperationException(
                    $"Tenant violation: entity {entry.Entity.GetType().Name} has ClientId={id} " +
                    $"but current tenant is ClientId={db.CurrentClientId}.");
            }
        }
    }
}
