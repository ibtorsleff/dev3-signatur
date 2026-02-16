using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SignaturPortal.Infrastructure.Data;

namespace SignaturPortal.Infrastructure.Localization;

/// <summary>
/// Hosted service that bulk-loads all enabled Localization rows into IMemoryCache at startup.
/// Matches the legacy CacheLocalization() behavior from Global.asax / FLCache.
/// Also injectable as a singleton for admin cache management (status + reload).
/// </summary>
public class LocalizationCacheWarmupService : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IMemoryCache _cache;
    private readonly ILogger<LocalizationCacheWarmupService> _logger;

    /// <summary>
    /// Number of localization entries loaded in the last warmup/reload.
    /// </summary>
    public int EntryCount { get; private set; }

    /// <summary>
    /// Timestamp of the last successful cache load/reload.
    /// </summary>
    public DateTime? LastLoadedAt { get; private set; }

    public LocalizationCacheWarmupService(
        IServiceScopeFactory scopeFactory,
        IMemoryCache cache,
        ILogger<LocalizationCacheWarmupService> logger)
    {
        _scopeFactory = scopeFactory;
        _cache = cache;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await ReloadAsync(cancellationToken);
    }

    /// <summary>
    /// Bulk-loads all enabled localization entries into the memory cache.
    /// Can be called at startup or on-demand for cache refresh.
    /// Returns the number of entries loaded.
    /// </summary>
    public async Task<int> ReloadAsync(CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<SignaturDbContext>>();
        await using var context = await dbFactory.CreateDbContextAsync(cancellationToken);

        var entries = await context.Localizations
            .Where(l => l.Enabled)
            .Select(l => new { l.Key, l.LanguageId, l.Value })
            .ToListAsync(cancellationToken);

        var cacheOptions = new MemoryCacheEntryOptions { Priority = CacheItemPriority.NeverRemove };

        foreach (var entry in entries)
        {
            var cacheKey = $"loc_{entry.LanguageId}_{entry.Key}";
            _cache.Set(cacheKey, entry.Value, cacheOptions);
        }

        EntryCount = entries.Count;
        LastLoadedAt = DateTime.UtcNow;

        _logger.LogInformation("Localization cache warmed: {Count} entries loaded", entries.Count);

        return entries.Count;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
