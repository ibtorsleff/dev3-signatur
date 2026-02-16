using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SignaturPortal.Application.Interfaces;
using SignaturPortal.Domain.Enums;
using SignaturPortal.Infrastructure.Data;

namespace SignaturPortal.Infrastructure.Localization;

/// <summary>
/// Provides localized text from the Localization database table, backed by IMemoryCache.
/// Cache is pre-warmed at startup by LocalizationCacheWarmupService.
/// On cache miss, queries the DB on-demand and caches the result.
/// Fallback chain: requested language -> English (1) -> [key].
/// </summary>
public class LocalizationService : ILocalizationService
{
    private readonly IMemoryCache _cache;
    private readonly IDbContextFactory<SignaturDbContext> _dbFactory;
    private readonly IUserSessionContext _session;

    private const int FallbackLanguageId = (int)AppLanguage.EN; // 1
    private const int DefaultLanguageId = (int)AppLanguage.DK;  // 3

    public LocalizationService(
        IMemoryCache cache,
        IDbContextFactory<SignaturDbContext> dbFactory,
        IUserSessionContext session)
    {
        _cache = cache;
        _dbFactory = dbFactory;
        _session = session;
    }

    public string GetText(string key)
    {
        var languageId = _session.UserLanguageId > 0 ? _session.UserLanguageId : DefaultLanguageId;
        return GetText(key, languageId);
    }

    public string GetText(string key, int languageId)
    {
        if (languageId <= 0)
            languageId = DefaultLanguageId;

        var cacheKey = $"loc_{languageId}_{key}";
        if (_cache.TryGetValue(cacheKey, out string? value))
            return value!;

        // Cache miss -- query DB on-demand
        value = QueryFromDatabase(key, languageId);
        if (value is not null)
        {
            _cache.Set(cacheKey, value, new MemoryCacheEntryOptions { Priority = CacheItemPriority.NeverRemove });
            return value;
        }

        // Fallback to English if requested language is not English
        if (languageId != FallbackLanguageId)
        {
            var fallbackCacheKey = $"loc_{FallbackLanguageId}_{key}";
            if (_cache.TryGetValue(fallbackCacheKey, out string? fallbackValue))
                return fallbackValue!;

            fallbackValue = QueryFromDatabase(key, FallbackLanguageId);
            if (fallbackValue is not null)
            {
                _cache.Set(fallbackCacheKey, fallbackValue, new MemoryCacheEntryOptions { Priority = CacheItemPriority.NeverRemove });
                return fallbackValue;
            }
        }

        // No translation found -- return bracketed key for debuggability
        return $"[{key}]";
    }

    public string GetText(string key, params object[] args)
    {
        var languageId = _session.UserLanguageId > 0 ? _session.UserLanguageId : DefaultLanguageId;
        return GetText(key, languageId, args);
    }

    public string GetText(string key, int languageId, params object[] args)
    {
        var rawValue = GetText(key, languageId);

        if (args is not { Length: > 0 })
            return rawValue;

        try
        {
            return string.Format(rawValue, args);
        }
        catch (FormatException)
        {
            // Format mismatch -- return unformatted value gracefully
            return rawValue;
        }
    }

    public bool TextExists(string key)
    {
        var languageId = _session.UserLanguageId > 0 ? _session.UserLanguageId : DefaultLanguageId;
        return TextExists(key, languageId);
    }

    public bool TextExists(string key, int languageId)
    {
        var result = GetText(key, languageId);
        return result != $"[{key}]";
    }

    private string? QueryFromDatabase(string key, int languageId)
    {
        using var context = _dbFactory.CreateDbContext();
        return context.Localizations
            .Where(l => l.Key == key && l.LanguageId == languageId && l.Enabled)
            .Select(l => l.Value)
            .FirstOrDefault();
    }
}
