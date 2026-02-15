# Phase 5: Localization & UX Polish - Research

**Researched:** 2026-02-15
**Domain:** Database-driven localization (IStringLocalizer), Blazor Server circuit resilience, UX loading/error states
**Confidence:** HIGH

## Summary

Phase 5 implements localization and UX polish for a Blazor Server e-recruitment portal that must match exact legacy WebForms behavior. The critical finding is that the legacy app uses a **database-driven GetText pattern** backed by a `Localization` table with ~13,700 enabled rows across 2 languages (Danish LanguageId=3, English LanguageId=1). The legacy `Globalization` class performs keyed lookups with in-memory caching and falls back from the requested language to English when a key is not found. The Blazor app must replicate this exact behavior through a custom `IStringLocalizer` implementation that reads from the same `Localization` table.

The localization architecture is straightforward: implement a custom `IStringLocalizer` and `IStringLocalizerFactory` backed by EF Core, register them with `AddLocalization()`, and inject `IStringLocalizer` into Blazor components. The `UserLanguageId` is already captured in the scoped `UserSessionContext` service from the legacy session (values: 1=EN, 3=DK). The key challenge is mapping the legacy's integer-based `LanguageId` to .NET's `CultureInfo`-based localization system, and ensuring format string placeholders (`{0}`, `{1}`) work identically to the legacy `string.Format` pattern.

For UX, the project already has a custom `ReconnectModal.razor` component with JS/CSS. Circuit configuration uses `CircuitOptions` for retention periods. MudBlazor provides built-in loading states, validation display, and error handling that align with the requirements. The .NET 10 `PersistentComponentState` (already used for session persistence) and the new circuit persistence features provide robust reconnection handling.

**Primary recommendation:** Implement a custom `DbStringLocalizer`/`DbStringLocalizerFactory` that reads from the existing `Localization` table with `IMemoryCache`, map `UserLanguageId` to `CultureInfo` via middleware, and use MudBlazor's built-in validation/loading patterns for UX requirements.

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| Microsoft.Extensions.Localization | 10.0 | IStringLocalizer abstraction | Built-in ASP.NET Core localization framework, supports custom implementations |
| Microsoft.Extensions.Caching.Memory | 10.0 | In-memory cache for localization strings | Built-in, lightweight, matches legacy FLCache behavior |
| MudBlazor | 8.x (already installed) | UI components, loading states, validation display | Already in project from Phase 3 |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| FluentValidation | 11.x+ (Phase 4) | Validation rules | Already planned for Phase 4, localized error messages |
| Blazilla | Latest (Phase 4) | FluentValidation-Blazor bridge | Already planned for Phase 4, validation display in forms |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Custom IStringLocalizer | OrchardCore PO localizer | PO files don't match our DB-driven pattern; custom is correct here |
| IMemoryCache | MemoryCache (bare) | IMemoryCache integrates with DI, supports expiration; bare MemoryCache doesn't |
| Custom MudLocalizer | MudBlazor.Translations NuGet | MudBlazor.Translations uses resx files; we need DB-backed strings |

**Installation:**
```bash
dotnet add src/SignaturPortal.Infrastructure/SignaturPortal.Infrastructure.csproj package Microsoft.Extensions.Localization
dotnet add src/SignaturPortal.Web/SignaturPortal.Web.csproj package Microsoft.Extensions.Localization
# Microsoft.Extensions.Caching.Memory is already included in ASP.NET Core
# MudBlazor already installed
```

## Architecture Patterns

### Recommended Project Structure
```
src/
├── SignaturPortal.Domain/
│   └── Enums/
│       └── AppLanguage.cs              # Language enum matching legacy (EN=1, DK=3)
├── SignaturPortal.Application/
│   └── Interfaces/
│       └── ILocalizationService.cs     # Application-layer abstraction
├── SignaturPortal.Infrastructure/
│   ├── Data/
│   │   └── Entities/
│   │       └── Localization.cs         # EF Core entity for Localization table
│   ├── Localization/
│   │   ├── DbStringLocalizer.cs        # Custom IStringLocalizer implementation
│   │   ├── DbStringLocalizerFactory.cs # Custom IStringLocalizerFactory
│   │   └── LocalizationCacheService.cs # Warm cache on startup, serve from memory
│   └── Data/
│       └── SignaturDbContext.cs         # Add DbSet<Localization>
└── SignaturPortal.Web/
    ├── Middleware/
    │   └── LanguageCultureMiddleware.cs # Set CultureInfo from UserLanguageId
    ├── Components/
    │   ├── Layout/
    │   │   ├── ReconnectModal.razor     # Already exists, enhance with branding
    │   │   └── MainLayout.razor         # Add error boundary
    │   └── Shared/
    │       └── LoadingIndicator.razor   # Reusable loading component
    └── Program.cs                       # Register localization services
```

### Pattern 1: Database-Backed IStringLocalizer
**What:** Custom IStringLocalizer that reads from the legacy Localization table with memory caching
**When to use:** All UI text that needs localization
**Example:**
```csharp
// Infrastructure/Localization/DbStringLocalizer.cs
public class DbStringLocalizer : IStringLocalizer
{
    private readonly IMemoryCache _cache;
    private readonly IDbContextFactory<SignaturDbContext> _dbFactory;
    private readonly string _cultureName;

    public DbStringLocalizer(
        IMemoryCache cache,
        IDbContextFactory<SignaturDbContext> dbFactory,
        string cultureName)
    {
        _cache = cache;
        _dbFactory = dbFactory;
        _cultureName = cultureName;
    }

    public LocalizedString this[string name]
    {
        get
        {
            var value = GetString(name);
            return new LocalizedString(name, value ?? name, value == null);
        }
    }

    public LocalizedString this[string name, params object[] arguments]
    {
        get
        {
            var value = GetString(name);
            if (value != null)
            {
                // Legacy uses string.Format with {0}, {1}, etc.
                value = string.Format(value, arguments);
            }
            return new LocalizedString(name, value ?? name, value == null);
        }
    }

    private string? GetString(string key)
    {
        int languageId = CultureToLanguageId(_cultureName);
        string cacheKey = $"Loc_{key}_{languageId}";

        if (_cache.TryGetValue(cacheKey, out string? cached))
            return cached == "N/A" ? null : cached;

        // Fallback chain: requested language -> English
        using var db = _dbFactory.CreateDbContext();
        var value = db.Localizations
            .Where(l => l.Key == key && l.LanguageId == languageId && l.Enabled)
            .Select(l => l.Value)
            .FirstOrDefault();

        if (value == null && languageId != 1) // 1 = EN fallback
        {
            value = db.Localizations
                .Where(l => l.Key == key && l.LanguageId == 1 && l.Enabled)
                .Select(l => l.Value)
                .FirstOrDefault();
        }

        _cache.Set(cacheKey, value ?? "N/A", TimeSpan.FromHours(1));
        return value;
    }

    private static int CultureToLanguageId(string culture) => culture switch
    {
        "da" or "da-DK" => 3,
        "de" or "de-DE" => 4,
        "es" or "es-ES" => 5,
        _ => 1 // EN default
    };

    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
        => Enumerable.Empty<LocalizedString>(); // Not needed for our use case
}
```

### Pattern 2: Culture Middleware (Session-Driven)
**What:** Set CultureInfo.CurrentCulture based on UserLanguageId from session
**When to use:** Every request, before localization middleware runs
**Example:**
```csharp
// Web/Middleware/LanguageCultureMiddleware.cs
public class LanguageCultureMiddleware
{
    private readonly RequestDelegate _next;

    public LanguageCultureMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, IUserSessionContext session)
    {
        if (session.IsInitialized && session.UserLanguageId > 0)
        {
            var culture = session.UserLanguageId switch
            {
                3 => new CultureInfo("da-DK"),
                4 => new CultureInfo("de-DE"),
                5 => new CultureInfo("es-ES"),
                _ => new CultureInfo("en-US")
            };
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
        }
        await _next(context);
    }
}
```

### Pattern 3: Warm Cache on Application Start
**What:** Pre-load all Localization rows into IMemoryCache at startup (matches legacy CacheLocalization behavior)
**When to use:** Application startup, to avoid N+1 DB queries on first page load
**Example:**
```csharp
// Infrastructure/Localization/LocalizationCacheService.cs
public class LocalizationCacheService : IHostedService
{
    private readonly IServiceProvider _services;
    private readonly IMemoryCache _cache;

    public LocalizationCacheService(IServiceProvider services, IMemoryCache cache)
    {
        _services = services;
        _cache = cache;
    }

    public async Task StartAsync(CancellationToken ct)
    {
        using var scope = _services.CreateScope();
        var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<SignaturDbContext>>();
        using var db = dbFactory.CreateDbContext();

        var entries = await db.Localizations
            .Where(l => l.Enabled)
            .Select(l => new { l.Key, l.LanguageId, l.Value })
            .ToListAsync(ct);

        foreach (var entry in entries)
        {
            _cache.Set($"Loc_{entry.Key}_{entry.LanguageId}", entry.Value,
                TimeSpan.FromHours(24));
        }
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
```

### Pattern 4: Component Usage
**What:** Inject IStringLocalizer into Blazor components
**When to use:** Every component that displays UI text
**Example:**
```razor
@inject IStringLocalizer Loc

<MudDataGrid T="ActivityListDto" ...>
    <ToolBarContent>
        <MudText Typo="Typo.h6">@Loc["RecruitmentActivities"]</MudText>
    </ToolBarContent>
    <Columns>
        <PropertyColumn Property="x => x.Headline" Title="@Loc["Activity"]" />
        <PropertyColumn Property="x => x.JournalNo" Title="@Loc["JournalNo"]" />
        <PropertyColumn Property="x => x.ApplicationDeadline"
                        Title="@Loc["Deadline"]" Format="yyyy-MM-dd" />
    </Columns>
    <NoRecordsContent>
        <MudText>@Loc["NoActiveSearch"]</MudText>
    </NoRecordsContent>
</MudDataGrid>
```

### Pattern 5: MudBlazor Component Localization
**What:** Custom MudLocalizer backed by the same DB localizer
**When to use:** Translating MudBlazor built-in strings (data grid pager, filters, etc.)
**Example:**
```csharp
// Web/Services/DbMudLocalizer.cs
internal class DbMudLocalizer : MudLocalizer
{
    private readonly IStringLocalizer _localizer;

    public DbMudLocalizer(IStringLocalizer localizer) => _localizer = localizer;

    public override LocalizedString this[string key] => _localizer[key];
    public override LocalizedString this[string key, params object[] arguments]
        => _localizer[key, arguments];
}

// Registration in Program.cs
builder.Services.AddTransient<MudLocalizer, DbMudLocalizer>();
```

### Anti-Patterns to Avoid
- **Do NOT use .resx resource files:** The legacy app stores all strings in the database. Using resx would create a parallel, unsynchronized localization system.
- **Do NOT query the DB per string lookup:** Always use the warmed cache. The legacy app loads all strings on Application_Start.
- **Do NOT set culture via cookie/query string:** The culture comes from the user's session `UserLanguageId`, not browser settings. The legacy app controls this.
- **Do NOT create a separate localization service interface:** Use the standard `IStringLocalizer` so that components remain framework-standard and testable.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| String localization framework | Custom string lookup API | IStringLocalizer + custom factory | Framework integration, @inject support, consistent API, testing |
| Memory caching | Dictionary<string, string> | IMemoryCache | Thread-safe, expiration, DI integration, size limits |
| Form validation display | Custom error rendering | MudBlazor validation + Blazilla | MudTextField has built-in Error/ErrorText, inline display |
| Loading indicators | Custom spinners | MudProgressLinear / MudProgressCircular / MudSkeleton | Theme-consistent, accessible, responsive |
| Circuit reconnection UI | Custom WebSocket handling | Built-in reconnect dialog (ReconnectModal.razor) | Already implemented with .NET 10 patterns, JS event system |
| Date/number formatting | Custom formatters | CultureInfo.CurrentCulture | .NET handles da-DK formatting natively |

**Key insight:** The localization domain is the one area where custom code is genuinely required (custom IStringLocalizer), because the data source is a legacy database table. Everything else should leverage existing frameworks.

## Common Pitfalls

### Pitfall 1: LanguageId-to-CultureInfo Mapping Mismatch
**What goes wrong:** The legacy app uses integer LanguageId values (1=EN, 3=DK) but .NET localization uses CultureInfo strings ("en-US", "da-DK"). Incorrect mapping breaks all localization.
**Why it happens:** The Language enum in the legacy app is non-contiguous (Default=0, EN=1, FR=2, DK=3, DE=4, ES=5). FR=2 appears in older code but only EN and DK exist in the Languages table.
**How to avoid:** Create a single authoritative mapping function. Verify against the Languages table: only LanguageId 1 (EN) and 3 (DK) have data in the Localization table.
**Warning signs:** UI text showing "N/A" or key names instead of translated text.

### Pitfall 2: Cache Not Warmed Before First Request
**What goes wrong:** First page load triggers hundreds of individual DB queries as each localization key is fetched independently.
**Why it happens:** IStringLocalizer is called during component rendering, before the IHostedService has finished loading the cache.
**How to avoid:** Use IHostedService to warm the cache at startup. The legacy app does this in Application_Start via `CacheLocalization()`.
**Warning signs:** Slow first page load, SQL query storms in profiler.

### Pitfall 3: Format String Placeholders Not Applied
**What goes wrong:** UI shows "{0} candidates" instead of "42 candidates".
**Why it happens:** The legacy GetText uses `string.Format(value, args)` with `{0}`, `{1}` placeholders. The IStringLocalizer indexer with params must apply the same formatting.
**How to avoid:** The `this[string name, params object[] arguments]` overload must call `string.Format(value, arguments)`.
**Warning signs:** Literal `{0}` appearing in the UI.

### Pitfall 4: Fallback Language Not Matching Legacy
**What goes wrong:** When a key has no Danish translation, the legacy app falls back to English (LanguageId=1). If the Blazor app doesn't replicate this, missing translations show "N/A" or the key itself.
**Why it happens:** The legacy `Globalization.Get()` method has an explicit fallback: if the requested language returns no result, it queries again with `Language.EN` (=1).
**How to avoid:** Implement the same two-step lookup in the custom IStringLocalizer: first try requested language, then try EN.
**Warning signs:** Missing translations for Danish keys that exist only in English.

### Pitfall 5: CultureInfo Not Set During SignalR Hub Calls
**What goes wrong:** After initial SSR, the Blazor circuit communicates via SignalR. The CultureInfo set by middleware during the HTTP request is not automatically propagated to subsequent SignalR calls.
**Why it happens:** Middleware only runs during HTTP requests, not WebSocket messages. The scoped UserSessionContext survives the circuit but CultureInfo.CurrentCulture does not.
**How to avoid:** Set CultureInfo in a circuit handler or use the scoped UserSessionContext's LanguageId directly in the DbStringLocalizer rather than relying on CultureInfo.CurrentUICulture.
**Warning signs:** Localization works on first page load (SSR) but reverts to default language during interactive navigation.

### Pitfall 6: Reconnect Modal Hardcoded in English
**What goes wrong:** The existing ReconnectModal.razor has English-only text ("Rejoining the server...").
**Why it happens:** The reconnect modal renders outside the Blazor circuit (it shows WHEN the circuit is disconnected), so IStringLocalizer injection may not work.
**How to avoid:** Pre-render localized strings into the HTML during SSR, or use a data attribute approach where localized values are embedded in the page on initial render and read by JavaScript.
**Warning signs:** Danish users seeing English reconnection messages.

## Code Examples

### Localization Entity (EF Core)
```csharp
// Infrastructure/Data/Entities/Localization.cs
// Matches legacy table: Localization
// Columns: Id, Area, Key, Value, SiteId, Enabled, LanguageId,
//          LocalizationTypeId, CreateDate, ModifiedDate, Approved
public class LocalizationEntry
{
    public int Id { get; set; }
    public string Area { get; set; } = string.Empty;      // varchar(50)
    public string Key { get; set; } = string.Empty;        // varchar(128)
    public string Value { get; set; } = string.Empty;      // nvarchar(max)
    public int SiteId { get; set; }                         // always -1 (global)
    public bool Enabled { get; set; }
    public int LanguageId { get; set; }                     // 1=EN, 3=DK
    public int LocalizationTypeId { get; set; }
    public DateTime CreateDate { get; set; }
    public DateTime ModifiedDate { get; set; }
    public bool Approved { get; set; }
}

// In DbContext OnModelCreating:
modelBuilder.Entity<LocalizationEntry>(entity =>
{
    entity.ToTable("Localization");
    entity.HasKey(e => e.Id);
    entity.Property(e => e.Key).HasMaxLength(128).IsUnicode(false);
    entity.Property(e => e.Area).HasMaxLength(50).IsUnicode(false);
    entity.Property(e => e.Value).IsRequired();
});
```

### Language Enum (Domain)
```csharp
// Domain/Enums/AppLanguage.cs
// Must match legacy GenericObjects.Language enum exactly
public enum AppLanguage : int
{
    Default = 0,
    EN = 1,    // English (LanguageId in DB)
    // FR = 2, // Not used - no data in DB
    DK = 3,    // Danish (LanguageId in DB)
    DE = 4,    // German
    ES = 5     // Spanish
}
```

### DI Registration (Program.cs)
```csharp
// In Program.cs - Localization setup
builder.Services.AddLocalization();
builder.Services.AddMemoryCache();

// Replace default IStringLocalizerFactory with DB-backed one
builder.Services.AddSingleton<IStringLocalizerFactory, DbStringLocalizerFactory>();

// Warm localization cache on startup
builder.Services.AddHostedService<LocalizationCacheService>();

// MudBlazor localization bridge
builder.Services.AddTransient<MudLocalizer, DbMudLocalizer>();

// In middleware pipeline (after UseSystemWebAdapters and UserSessionMiddleware):
app.UseMiddleware<LanguageCultureMiddleware>();
app.UseRequestLocalization(new RequestLocalizationOptions()
    .SetDefaultCulture("da-DK")
    .AddSupportedCultures("da-DK", "en-US")
    .AddSupportedUICultures("da-DK", "en-US"));
```

### Circuit Configuration
```csharp
// In Program.cs - Circuit options
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddHubOptions(options =>
    {
        options.MaximumReceiveMessageSize = 10 * 1024 * 1024;
    });

// Circuit retention and persistence
builder.Services.Configure<Microsoft.AspNetCore.Components.Server.CircuitOptions>(options =>
{
    options.DisconnectedCircuitRetentionPeriod = TimeSpan.FromMinutes(5);
    options.DisconnectedCircuitMaxRetained = 100;
    // .NET 10: persisted circuit retention (state survives beyond disconnect)
    options.PersistedCircuitInMemoryRetentionPeriod = TimeSpan.FromHours(2);
});
```

### Error Boundary in MainLayout
```razor
@* MainLayout.razor - add ErrorBoundary around Body *@
@inherits LayoutComponentBase

<SessionPersistence />

<div class="page">
    <NavMenu />
    <main class="main-content">
        <ErrorBoundary @ref="errorBoundary">
            <ChildContent>
                @Body
            </ChildContent>
            <ErrorContent>
                <MudAlert Severity="Severity.Error" Class="my-4">
                    @Loc["UnexpectedError"]
                    <MudButton OnClick="Recover" Variant="Variant.Text"
                               Color="Color.Error">@Loc["TryAgain"]</MudButton>
                </MudAlert>
            </ErrorContent>
        </ErrorBoundary>
    </main>
</div>

@code {
    private ErrorBoundary? errorBoundary;
    private void Recover() => errorBoundary?.Recover();
}
```

### Loading Indicator Pattern
```razor
@* Reusable loading pattern with MudBlazor *@
@if (_loading)
{
    <MudProgressLinear Color="Color.Primary" Indeterminate="true" Class="my-4" />
}
else if (_error != null)
{
    <MudAlert Severity="Severity.Error" Class="my-4">@_error</MudAlert>
}
else
{
    @* Content here *@
}
```

### Inline Validation with MudBlazor + FluentValidation
```razor
@* MudTextField displays validation errors inline automatically *@
<MudForm @ref="form" Validation="@(new FluentValueValidator<string>(
    v => v.NotEmpty().WithMessage(Loc["FieldRequired"])))">
    <MudTextField @bind-Value="model.Headline"
                  Label="@Loc["Headline"]"
                  Immediate="true"
                  Validation="@(new FluentValueValidator<string>(
                      v => v.NotEmpty().WithMessage(Loc["HeadlineRequired"])))" />
</MudForm>
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| .resx resource files | Custom IStringLocalizer for DB | Always available | DB source matches legacy architecture |
| Blazored.FluentValidation | Blazilla | Dec 2025 | Blazored archived, Blazilla is successor |
| Manual circuit retry | .NET 10 built-in reconnect/pause/resume | .NET 10 | ReconnectModal with JS events handles lifecycle |
| DisconnectedCircuitRetentionPeriod only | PersistedCircuitInMemoryRetentionPeriod | .NET 10 | State survives circuit eviction, restores on reconnect |
| PersistentComponentState manual registration | [PersistentState] attribute | .NET 10 | Simpler API for state persistence |

**Deprecated/outdated:**
- Blazored.FluentValidation: Archived December 2025, use Blazilla
- Manual `ComponentApplicationState` events: Replaced by `[PersistentState]` attribute in .NET 10

## Open Questions

1. **MudBlazor internal strings localization keys**
   - What we know: MudBlazor uses keys like `MudDataGrid_Contains`, `MudDataGrid_IsEmpty` for built-in component text
   - What's unclear: The exact set of MudBlazor localization keys that need Danish translations in the DB
   - Recommendation: During implementation, capture all MudBlazor keys used and either add them to the Localization DB table or provide a fallback dictionary in the DbMudLocalizer

2. **CultureInfo propagation in SignalR circuits**
   - What we know: HTTP middleware sets CultureInfo during SSR. SignalR messages bypass middleware.
   - What's unclear: Whether .NET 10's Blazor Server automatically preserves CultureInfo across the circuit lifecycle
   - Recommendation: Use the scoped UserSessionContext.UserLanguageId as the authoritative language source in DbStringLocalizer, rather than CultureInfo.CurrentUICulture. This avoids the propagation issue entirely.

3. **Reconnect modal localization during disconnect**
   - What we know: The reconnect modal shows when the circuit is disconnected, meaning Blazor components can't inject services
   - What's unclear: Whether the JS-rendered reconnect dialog can be localized dynamically
   - Recommendation: Embed localized strings as data attributes during SSR initial render, read them from JavaScript

## Sources

### Primary (HIGH confidence)
- Legacy codebase `Atlanta.Common.Globalization` class - direct source code inspection of GetText/Get pattern
- Legacy DB `Localization` table - direct SQL queries confirming schema, row counts (6492 DK, 7222 EN), and key patterns
- Legacy DB `Languages` table - confirmed only LanguageId 1 (EN) and 3 (DK) exist
- Legacy `GenericObjects.Language` enum - confirmed values: Default=0, EN=1, DK=3, DE=4, ES=5
- Microsoft Learn: [Localization Extensibility](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/localization-extensibility) - IStringLocalizer custom implementation guidance
- Microsoft Learn: [Blazor globalization and localization](https://learn.microsoft.com/en-us/aspnet/core/blazor/globalization-localization?view=aspnetcore-10.0) - Blazor Server localization patterns
- GitHub dotnet/aspnetcore: [CircuitOptions.cs](https://github.com/dotnet/aspnetcore/blob/main/src/Components/Server/src/CircuitOptions.cs) - all circuit configuration properties confirmed

### Secondary (MEDIUM confidence)
- [MudBlazor localization docs](https://mudblazor.com/features/localization) - MudLocalizer custom implementation pattern
- [Telerik: .NET 10 circuit persistence](https://www.telerik.com/blogs/net-10-preview-release-6-tackles-blazor-server-lost-state-problem) - PersistedCircuitInMemoryRetentionPeriod behavior
- [MudBlazor.Translations](https://github.com/MudBlazor/Translations) - Reference for MudBlazor localization key names

### Tertiary (LOW confidence)
- None - all findings verified with at least two sources

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH - Using built-in ASP.NET Core localization framework with custom IStringLocalizer; well-documented pattern
- Architecture: HIGH - Legacy code examined directly; Localization table schema confirmed via SQL; GetText pattern fully understood
- Pitfalls: HIGH - CultureInfo propagation issue is well-known in Blazor Server; cache warming pattern confirmed from legacy Application_Start code
- Circuit config: MEDIUM - .NET 10 circuit persistence features are new; PersistedCircuitInMemoryRetentionPeriod confirmed but runtime behavior needs validation

**Research date:** 2026-02-15
**Valid until:** 2026-03-15 (30 days - stable domain, legacy DB schema is immutable)
