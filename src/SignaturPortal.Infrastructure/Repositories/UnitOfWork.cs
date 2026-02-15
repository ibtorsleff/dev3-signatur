using Microsoft.EntityFrameworkCore;
using SignaturPortal.Application.Interfaces;
using SignaturPortal.Domain.Interfaces;
using SignaturPortal.Infrastructure.Data;

namespace SignaturPortal.Infrastructure.Repositories;

/// <summary>
/// Unit of Work implementation that owns a DbContext instance.
/// Created via IDbContextFactory for Blazor Server circuit safety.
/// Stamps tenant context (SiteId/ClientId) on the DbContext so that
/// global query filters automatically scope all queries.
/// </summary>
public class UnitOfWork : IUnitOfWork
{
    private readonly SignaturDbContext _context;

    public UnitOfWork(IDbContextFactory<SignaturDbContext> contextFactory, IUserSessionContext session)
    {
        _context = contextFactory.CreateDbContext();
        if (session.IsInitialized)
        {
            _context.CurrentSiteId = session.SiteId;
            _context.CurrentClientId = session.ClientId;
        }
    }

    /// <summary>
    /// Gets a repository for the specified entity type.
    /// Repository shares this UnitOfWork's DbContext for transaction coordination.
    /// </summary>
    public IRepository<T> Repository<T>() where T : class
    {
        return new Repository<T>(_context);
    }

    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        return await _context.SaveChangesAsync(ct);
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
    }
}
