using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SignaturPortal.Application.Interfaces;
using SignaturPortal.Infrastructure.Data;
using SignaturPortal.Infrastructure.Localization;

namespace SignaturPortal.Tests.Localization;

/// <summary>
/// TUnit tests for LocalizationService covering: cache hit, cache miss with DB fallback,
/// missing key bracket notation, English fallback, user language resolution,
/// UserLanguageId=0 default, string.Format args, bad format args safety, TextExists.
/// </summary>
public class LocalizationServiceTests : IDisposable
{
    private readonly MemoryCache _cache;
    private readonly SqliteConnection _connection;

    public LocalizationServiceTests()
    {
        _cache = new MemoryCache(new MemoryCacheOptions());

        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        // Create just the Localization table via raw SQL to avoid SQLite incompatibilities
        // with SQL Server types (varbinary(max), ntext, etc.) from the full SignaturDbContext model.
        // SQLite is type-flexible on queries so EF Core queries work fine against this table.
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE Localization (
                Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                Area TEXT,
                [Key] TEXT NOT NULL,
                Value TEXT,
                SiteId INTEGER NOT NULL,
                Enabled INTEGER NOT NULL,
                LanguageId INTEGER NOT NULL,
                LocalizationTypeId INTEGER NOT NULL DEFAULT 0,
                CreateDate TEXT NOT NULL DEFAULT (datetime('now')),
                ModifiedDate TEXT NOT NULL DEFAULT (datetime('now')),
                Approved INTEGER NOT NULL DEFAULT 0
            )";
        cmd.ExecuteNonQuery();
    }

    private LocalizationService CreateService(int userLanguageId = 3, bool seedDb = false)
    {
        if (seedDb)
            SeedLocalizationData();

        var factory = new TestLocalizationDbContextFactory(_connection);
        var session = new StubUserSessionContext(userLanguageId);
        return new LocalizationService(_cache, factory, session);
    }

    private void SeedLocalizationData()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO Localization ([Key], LanguageId, SiteId, Value, Enabled, Area)
            VALUES ('DbKey', 3, 1, 'FraDb', 1, 'test');
            INSERT INTO Localization ([Key], LanguageId, SiteId, Value, Enabled, Area)
            VALUES ('FallbackKey', 1, 1, 'English Fallback', 1, 'test');
        ";
        cmd.ExecuteNonQuery();
    }

    [Test]
    public async Task GetText_WithCachedKey_ReturnsCachedValue()
    {
        _cache.Set("loc_3_TestKey", "TestVaerdi", new MemoryCacheEntryOptions { Priority = CacheItemPriority.NeverRemove });
        var svc = CreateService();

        var result = svc.GetText("TestKey", 3);

        await Assert.That(result).IsEqualTo("TestVaerdi");
    }

    [Test]
    public async Task GetText_WithCacheMiss_QueriesDbAndCachesResult()
    {
        var svc = CreateService(seedDb: true);

        var result = svc.GetText("DbKey", 3);

        await Assert.That(result).IsEqualTo("FraDb");

        // Verify the value is now cached
        var cached = _cache.TryGetValue("loc_3_DbKey", out string? cachedValue);
        await Assert.That(cached).IsTrue();
        await Assert.That(cachedValue).IsEqualTo("FraDb");
    }

    [Test]
    public async Task GetText_WithMissingKey_ReturnsBracketedKeyName()
    {
        var svc = CreateService();

        var result = svc.GetText("NonExistent", 3);

        await Assert.That(result).IsEqualTo("[NonExistent]");
    }

    [Test]
    public async Task GetText_FallsBackToEnglish_WhenRequestedLanguageMissing()
    {
        var svc = CreateService(seedDb: true);

        // FallbackKey exists only for LanguageId=1 (English), not 3 (Danish)
        var result = svc.GetText("FallbackKey", 3);

        await Assert.That(result).IsEqualTo("English Fallback");
    }

    [Test]
    public async Task GetText_NoArgs_UsesUserLanguageId()
    {
        _cache.Set("loc_3_SessionKey", "DanishText", new MemoryCacheEntryOptions { Priority = CacheItemPriority.NeverRemove });
        var svc = CreateService(userLanguageId: 3);

        var result = svc.GetText("SessionKey");

        await Assert.That(result).IsEqualTo("DanishText");
    }

    [Test]
    public async Task GetText_UserLanguageIdZero_DefaultsToDanish()
    {
        _cache.Set("loc_3_DefaultKey", "DanishDefault", new MemoryCacheEntryOptions { Priority = CacheItemPriority.NeverRemove });
        var svc = CreateService(userLanguageId: 0);

        var result = svc.GetText("DefaultKey");

        await Assert.That(result).IsEqualTo("DanishDefault");
    }

    [Test]
    public async Task GetText_WithFormatArgs_AppliesStringFormat()
    {
        _cache.Set("loc_3_FormatKey", "Hello {0}, you have {1} items", new MemoryCacheEntryOptions { Priority = CacheItemPriority.NeverRemove });
        var svc = CreateService();

        var result = svc.GetText("FormatKey", 3, "Alice", 5);

        await Assert.That(result).IsEqualTo("Hello Alice, you have 5 items");
    }

    [Test]
    public async Task GetText_WithBadFormatArgs_ReturnsUnformattedValue()
    {
        _cache.Set("loc_3_BadFormat", "No placeholders here", new MemoryCacheEntryOptions { Priority = CacheItemPriority.NeverRemove });
        var svc = CreateService();

        var result = svc.GetText("BadFormat", 3, "extra", "args");

        await Assert.That(result).IsEqualTo("No placeholders here");
    }

    [Test]
    public async Task TextExists_ReturnsTrueForExistingKey()
    {
        _cache.Set("loc_3_TestKey", "SomeValue", new MemoryCacheEntryOptions { Priority = CacheItemPriority.NeverRemove });
        var svc = CreateService();

        var result = svc.TextExists("TestKey");

        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task TextExists_ReturnsFalseForMissingKey()
    {
        var svc = CreateService();

        var result = svc.TextExists("Missing");

        await Assert.That(result).IsFalse();
    }

    public void Dispose()
    {
        _cache.Dispose();
        _connection.Dispose();
    }

    /// <summary>
    /// Stub IDbContextFactory that creates SignaturDbContext instances on the shared
    /// SQLite in-memory connection. The Localization table is pre-created via raw SQL.
    /// </summary>
    private class TestLocalizationDbContextFactory : IDbContextFactory<SignaturDbContext>
    {
        private readonly SqliteConnection _connection;

        public TestLocalizationDbContextFactory(SqliteConnection connection)
        {
            _connection = connection;
        }

        public SignaturDbContext CreateDbContext()
        {
            var options = new DbContextOptionsBuilder<SignaturDbContext>()
                .UseSqlite(_connection)
                .Options;
            return new SignaturDbContext(options);
        }
    }

    /// <summary>
    /// Stub IUserSessionContext for testing language resolution.
    /// </summary>
    private class StubUserSessionContext : IUserSessionContext
    {
        public StubUserSessionContext(int userLanguageId)
        {
            UserLanguageId = userLanguageId;
        }

        public int? UserId => 1;
        public int? SiteId => 1;
        public int? ClientId => 10;
        public string UserName => "TestUser";
        public int UserLanguageId { get; }
        public bool IsInitialized => true;
        public bool IsClientUser => ClientId.HasValue && ClientId.Value > 0;
    }
}
