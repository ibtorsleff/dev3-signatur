namespace SignaturPortal.Domain.Interfaces;

/// <summary>
/// Unit of Work interface for transaction coordination.
/// Implementations in Infrastructure layer manage DbContext lifetime.
/// </summary>
public interface IUnitOfWork : IAsyncDisposable
{
    /// <summary>
    /// Persists all pending changes to the database.
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
