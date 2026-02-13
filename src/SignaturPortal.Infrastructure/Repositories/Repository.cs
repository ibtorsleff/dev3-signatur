using Microsoft.EntityFrameworkCore;
using SignaturPortal.Domain.Interfaces;
using SignaturPortal.Infrastructure.Data;

namespace SignaturPortal.Infrastructure.Repositories;

/// <summary>
/// Generic repository implementation using EF Core.
/// Each instance creates its own DbContext via IDbContextFactory for Blazor Server circuit safety.
/// </summary>
public class Repository<T> : IRepository<T> where T : class
{
    protected readonly SignaturDbContext Context;
    protected readonly DbSet<T> DbSet;

    public Repository(SignaturDbContext context)
    {
        Context = context;
        DbSet = context.Set<T>();
    }

    public virtual async Task<T?> GetByIdAsync(object id, CancellationToken ct = default)
    {
        return await DbSet.FindAsync(new[] { id }, ct);
    }

    public virtual async Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default)
    {
        return await DbSet.AsNoTracking().ToListAsync(ct);
    }

    public virtual async Task<T> AddAsync(T entity, CancellationToken ct = default)
    {
        var entry = await DbSet.AddAsync(entity, ct);
        return entry.Entity;
    }

    public virtual void Update(T entity)
    {
        DbSet.Update(entity);
    }

    public virtual void Remove(T entity)
    {
        DbSet.Remove(entity);
    }
}
