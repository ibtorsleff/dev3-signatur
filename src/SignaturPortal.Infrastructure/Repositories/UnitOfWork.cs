using Microsoft.EntityFrameworkCore;
using SignaturPortal.Domain.Interfaces;
using SignaturPortal.Infrastructure.Data;

namespace SignaturPortal.Infrastructure.Repositories;

/// <summary>
/// Unit of Work implementation that owns a DbContext instance.
/// Created via IDbContextFactory for Blazor Server circuit safety.
/// Dispose the UnitOfWork to dispose the underlying DbContext.
/// </summary>
public class UnitOfWork : IUnitOfWork
{
    private readonly SignaturDbContext _context;

    public UnitOfWork(IDbContextFactory<SignaturDbContext> contextFactory)
    {
        _context = contextFactory.CreateDbContext();
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
