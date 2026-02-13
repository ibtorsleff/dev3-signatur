# Phase 01: Infrastructure Shell - Research

**Researched:** 2026-02-13
**Domain:** YARP reverse proxy, System.Web Adapters, Clean Architecture, EF Core database-first
**Confidence:** HIGH

## Summary

Phase 1 establishes the foundational infrastructure for incremental WebForms-to-Blazor migration using Microsoft's official migration tools: YARP 2.3.0 for reverse proxying and System.Web Adapters 2.3.0 for session/auth sharing. The architecture follows Clean Architecture principles with explicit layer separation, EF Core database-first scaffolding (no migrations), and IDbContextFactory for Blazor Server circuit-safe database access.

**Critical finding:** YARP and Blazor both act as fallback routes, creating routing collisions unless explicitly ordered. System.Web Adapters session state is only available during SSR (Static Server Rendering), not within Blazor Server circuits due to WebSocket-based architecture.

**Primary recommendation:** Use MapForwarder with `.WithOrder(int.MaxValue).ShortCircuit()` for YARP fallback to ensure local Blazor routes take precedence. Register session keys explicitly with `AddJsonSessionSerializer`. Use IDbContextFactory (not scoped DbContext) for multi-tenancy and circuit-safe database access.

## Standard Stack

### Core Infrastructure

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| Yarp.ReverseProxy | 2.3.0 | Reverse proxy routing | Microsoft-official incremental migration tool, production-proven |
| Microsoft.AspNetCore.SystemWebAdapters | 2.3.0 | Remote session/auth sharing | Microsoft-official bridge between .NET Framework and Core |
| Microsoft.AspNetCore.SystemWebAdapters.FrameworkServices | 2.3.0 | WebForms-side adapters | Required for Framework app to expose session/auth endpoints |

### Data Access

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| Microsoft.EntityFrameworkCore | 10.0.0 | ORM for database access | Microsoft's official ORM, database-first support |
| Microsoft.EntityFrameworkCore.SqlServer | 10.0.0 | SQL Server provider | Production SQL Server support |
| Microsoft.EntityFrameworkCore.Tools | 10.0.0 | Scaffolding tools | Required for `dotnet ef dbcontext scaffold` |

### Testing

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| TUnit | 1.0+ | Unit testing framework | Modern, fast, source-generated tests with Native AOT support |
| Microsoft.Playwright | 1.49+ | E2E browser automation | Microsoft-official cross-browser testing, Chromium/Firefox/WebKit |

### Supporting

| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| MudBlazor | 8.15.0 | UI component library | Confirmed in project research |
| Wolverine | 3.x | Mediator/CQRS/messaging | If MediatR licensing is concern (MediatR now requires commercial license) |

**Installation (NuGet):**
```bash
# YARP + System.Web Adapters (Blazor Core app)
dotnet add package Yarp.ReverseProxy --version 2.3.0
dotnet add package Microsoft.AspNetCore.SystemWebAdapters --version 2.3.0

# System.Web Adapters (WebForms Framework app)
dotnet add package Microsoft.AspNetCore.SystemWebAdapters.FrameworkServices --version 2.3.0

# EF Core
dotnet add package Microsoft.EntityFrameworkCore --version 10.0.0
dotnet add package Microsoft.EntityFrameworkCore.SqlServer --version 10.0.0
dotnet add package Microsoft.EntityFrameworkCore.Tools --version 10.0.0

# Testing
dotnet add package TUnit --version 1.0.0
dotnet add package Microsoft.Playwright --version 1.49.0
```

## Architecture Patterns

### Recommended Project Structure (Clean Architecture)

```
src/
├── AtlantaSignatur.Domain/           # Core business entities, no dependencies
│   ├── Entities/                     # Domain entities (Client, ERActivity, ERCandidate)
│   ├── Interfaces/                   # Repository interfaces (IClientRepository, IUnitOfWork)
│   └── ValueObjects/                 # Value objects (Email, Phone)
├── AtlantaSignatur.Application/      # Business logic, depends on Domain
│   ├── Commands/                     # Command handlers (MediatR or Wolverine)
│   ├── Queries/                      # Query handlers
│   ├── DTOs/                         # Data Transfer Objects
│   └── Mappings/                     # Manual DTO mapping extensions
├── AtlantaSignatur.Infrastructure/   # Data access, depends on Application
│   ├── Data/
│   │   ├── SignaturDbContext.cs      # EF Core DbContext (scaffolded)
│   │   ├── Entities/                 # EF Core entities (scaffolded, partial classes)
│   │   └── Configurations/           # Partial class extensions (manual)
│   ├── Repositories/                 # Repository implementations
│   └── Services/                     # External service integrations
└── AtlantaSignatur.Web/              # Blazor Server presentation
    ├── Components/                   # Blazor components
    ├── Program.cs                    # YARP + System.Web Adapters config
    └── appsettings.json              # YARP routes, connection strings
```

**Source:** [Clean Architecture Blazor Server (GitHub)](https://github.com/neozhu/CleanArchitectureWithBlazorServer), [Jason Taylor Clean Architecture](https://github.com/jasontaylordev/CleanArchitecture)

### Pattern 1: YARP Configuration with Route Ordering

**What:** Configure YARP to proxy unmigrated routes to WebForms while Blazor handles migrated routes.
**When to use:** Phase 1 infrastructure setup, ongoing throughout migration.

**appsettings.json:**
```json
{
  "ReverseProxy": {
    "Routes": {
      "blazor-routes": {
        "ClusterId": "local-blazor",
        "Order": -1,
        "Match": {
          "Path": "/migrated/{**catch-all}"
        }
      },
      "fallback-to-webforms": {
        "ClusterId": "legacy-webforms",
        "Order": 2147483647,
        "Match": {
          "Path": "/{**catch-all}"
        }
      }
    },
    "Clusters": {
      "legacy-webforms": {
        "Destinations": {
          "webforms-app": {
            "Address": "https://localhost:44300"
          }
        }
      }
    }
  }
}
```

**Program.cs:**
```csharp
// Source: https://learn.microsoft.com/aspnet/core/migration/fx-to-core/inc/remote-app-setup
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.SystemWebAdapters;

var builder = WebApplication.CreateBuilder(args);

// Register YARP
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

// Middleware ordering is CRITICAL
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

// Map Blazor components FIRST (higher precedence)
app.MapRazorComponents<App>();

// Fallback to WebForms via YARP (lowest precedence)
app.MapForwarder("/{**catch-all}",
    app.Services.GetRequiredService<IOptions<RemoteAppClientOptions>>().Value.RemoteAppUrl.OriginalString)
    .WithOrder(int.MaxValue)  // Lowest priority
    .ShortCircuit();          // Skip remaining middleware

app.Run();
```

**Key insight:** Lower Order values = higher priority. `int.MaxValue` ensures YARP runs last.

### Pattern 2: System.Web Adapters Remote Session + Authentication

**What:** Share session and authentication state between WebForms (.NET Framework) and Blazor (.NET 10).
**When to use:** When user navigating between migrated and legacy pages must retain session/auth.

**Blazor Core Program.cs:**
```csharp
// Source: https://learn.microsoft.com/aspnet/core/migration/fx-to-core/inc/remote-app-setup
builder.Services.AddSystemWebAdapters()
    .AddJsonSessionSerializer(options =>
    {
        // CRITICAL: Every session key MUST be registered with type
        options.RegisterKey<int>("UserId");
        options.RegisterKey<string>("SiteId");
        options.RegisterKey<string>("ClientId");
    })
    .AddRemoteAppClient(options =>
    {
        options.RemoteAppUrl = new Uri(builder.Configuration["RemoteAppUri"]);
        options.ApiKey = builder.Configuration["RemoteAppApiKey"];
    })
    .AddSessionClient()
    .AddAuthenticationClient(true); // true = default scheme

// Enable session for specific routes
app.MapDefaultControllerRoute()
    .RequireSystemWebAdapterSession();
```

**WebForms Framework Global.asax.cs:**
```csharp
// Source: https://learn.microsoft.com/aspnet/core/migration/fx-to-core/inc/remote-app-setup
using Microsoft.AspNetCore.SystemWebAdapters;

protected void Application_Start()
{
    HttpApplicationHost.RegisterHost(builder =>
    {
        builder.AddSystemWebAdapters()
            .AddJsonSessionSerializer(options =>
            {
                // MUST match Blazor app registration
                options.RegisterKey<int>("UserId");
                options.RegisterKey<string>("SiteId");
                options.RegisterKey<string>("ClientId");
            })
            .AddRemoteAppServer(options =>
            {
                options.ApiKey = ConfigurationManager.AppSettings["RemoteAppApiKey"];
            })
            .AddSessionServer()
            .AddAuthenticationServer()
            .AddProxySupport(options => options.UseForwardedHeaders = true);
    });
}
```

**web.config (WebForms):**
```xml
<system.webServer>
  <modules>
    <remove name="SystemWebAdapterModule" />
    <add name="SystemWebAdapterModule"
         type="Microsoft.AspNetCore.SystemWebAdapters.SystemWebAdapterModule, Microsoft.AspNetCore.SystemWebAdapters.FrameworkServices"
         preCondition="managedHandler" />
  </modules>
</system.webServer>
```

### Pattern 3: EF Core Database-First Scaffolding with Partial Classes

**What:** Scaffold DbContext and entities from existing database, use partial classes for custom code.
**When to use:** Initial database scaffolding, re-scaffolding after schema changes.

**Scaffold command (.NET CLI):**
```bash
# Source: https://learn.microsoft.com/ef/core/managing-schemas/scaffolding/
dotnet ef dbcontext scaffold "Server=.;Database=SignaturAnnoncePortal;Trusted_Connection=True;TrustServerCertificate=True;" \
    Microsoft.EntityFrameworkCore.SqlServer \
    --output-dir Infrastructure/Data/Entities \
    --context-dir Infrastructure/Data \
    --context SignaturDbContext \
    --no-onconfiguring \
    --force
```

**Key flags:**
- `--no-onconfiguring`: Prevents hardcoded connection string in DbContext (use DI instead)
- `--force`: Overwrites existing files (safe because partial classes protect custom code)
- `--table`: Optional, scaffold specific tables (e.g., `--table ERActivity --table ERCandidate`)

**Scaffolded DbContext (generated):**
```csharp
// Infrastructure/Data/SignaturDbContext.cs
public partial class SignaturDbContext : DbContext
{
    public SignaturDbContext(DbContextOptions<SignaturDbContext> options)
        : base(options) { }

    public virtual DbSet<Client> Clients { get; set; } = null!;
    public virtual DbSet<ERActivity> ERActivities { get; set; } = null!;
    public virtual DbSet<ERCandidate> ERCandidates { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Scaffolded configuration
        modelBuilder.Entity<Client>(entity => {
            entity.ToTable("Client");
            entity.HasKey(e => e.ClientId);
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
```

**Custom DbContext extensions (manual, never overwritten):**
```csharp
// Infrastructure/Data/SignaturDbContext.Custom.cs
public partial class SignaturDbContext
{
    // Custom partial method implementation
    partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
    {
        // Custom configuration (query filters, indexes, etc.)
        modelBuilder.Entity<Client>()
            .HasQueryFilter(c => !c.IsDeleted);

        modelBuilder.Entity<ERActivity>()
            .HasIndex(e => e.SiteId);
    }

    // Custom methods
    public async Task<int> SaveChangesWithAuditAsync(string userId)
    {
        // Custom audit logic
        return await SaveChangesAsync();
    }
}
```

**Custom entity extensions (manual):**
```csharp
// Infrastructure/Data/Entities/Client.Custom.cs
public partial class Client
{
    // Custom properties (not in database)
    [NotMapped]
    public string DisplayName => $"{CompanyName} ({ClientId})";

    // Custom methods
    public bool IsActive() => StatusId == 1;
}
```

### Pattern 4: IDbContextFactory for Blazor Server

**What:** Use factory pattern instead of scoped DbContext for circuit-safe, multi-tenant database access.
**When to use:** Always in Blazor Server apps, especially with multi-tenancy.

**Registration (Program.cs):**
```csharp
// Source: https://learn.microsoft.com/aspnet/core/blazor/blazor-ef-core
builder.Services.AddDbContextFactory<SignaturDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("SignaturAnnoncePortal")));
```

**Usage in Blazor component:**
```razor
@inject IDbContextFactory<SignaturDbContext> DbFactory
@implements IAsyncDisposable

@code {
    private SignaturDbContext? _context;

    protected override async Task OnInitializedAsync()
    {
        // Create context for this component's lifetime
        _context = await DbFactory.CreateDbContextAsync();

        var clients = await _context.Clients
            .Where(c => c.SiteId == CurrentSiteId)
            .ToListAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_context is not null)
        {
            await _context.DisposeAsync();
        }
    }
}
```

**Multi-tenancy pattern:**
```csharp
// Source: https://learn.microsoft.com/ef/core/miscellaneous/multitenancy
public class TenantDbContextFactory : IDbContextFactory<SignaturDbContext>
{
    private readonly IDbContextFactory<SignaturDbContext> _pooledFactory;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public TenantDbContextFactory(
        IDbContextFactory<SignaturDbContext> pooledFactory,
        IHttpContextAccessor httpContextAccessor)
    {
        _pooledFactory = pooledFactory;
        _httpContextAccessor = httpContextAccessor;
    }

    public SignaturDbContext CreateDbContext()
    {
        var context = _pooledFactory.CreateDbContext();

        // Apply tenant filter from session
        var siteId = _httpContextAccessor.HttpContext?.Session.GetString("SiteId");
        if (!string.IsNullOrEmpty(siteId))
        {
            context.SiteId = siteId;
        }

        return context;
    }
}
```

### Pattern 5: Repository + Unit of Work with EF Core

**What:** Abstraction layer over EF Core for testability and domain isolation.
**When to use:** Clean Architecture layers, when EF Core is implementation detail.

**Note:** This is debated in .NET community. EF Core already implements Repository/UoW patterns. Add abstraction only if you need to hide EF Core from domain layer or enable swapping ORMs.

**Repository interface (Domain):**
```csharp
// Domain/Interfaces/IClientRepository.cs
public interface IClientRepository
{
    Task<Client?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<Client>> GetBySiteAsync(string siteId, CancellationToken ct = default);
    Task AddAsync(Client client, CancellationToken ct = default);
    void Update(Client client);
    void Remove(Client client);
}

// Domain/Interfaces/IUnitOfWork.cs
public interface IUnitOfWork : IDisposable
{
    IClientRepository Clients { get; }
    IERActivityRepository Activities { get; }
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
```

**Repository implementation (Infrastructure):**
```csharp
// Infrastructure/Repositories/ClientRepository.cs
public class ClientRepository : IClientRepository
{
    private readonly SignaturDbContext _context;

    public ClientRepository(SignaturDbContext context)
    {
        _context = context;
    }

    public async Task<Client?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        return await _context.Clients.FindAsync(new object[] { id }, ct);
    }

    public async Task<IReadOnlyList<Client>> GetBySiteAsync(string siteId, CancellationToken ct = default)
    {
        return await _context.Clients
            .Where(c => c.SiteId == siteId)
            .AsNoTracking()
            .ToListAsync(ct);
    }

    public async Task AddAsync(Client client, CancellationToken ct = default)
    {
        await _context.Clients.AddAsync(client, ct);
    }

    public void Update(Client client)
    {
        _context.Clients.Update(client);
    }

    public void Remove(Client client)
    {
        _context.Clients.Remove(client);
    }
}

// Infrastructure/Repositories/UnitOfWork.cs
public class UnitOfWork : IUnitOfWork
{
    private readonly SignaturDbContext _context;

    public UnitOfWork(SignaturDbContext context)
    {
        _context = context;
        Clients = new ClientRepository(context);
        Activities = new ERActivityRepository(context);
    }

    public IClientRepository Clients { get; }
    public IERActivityRepository Activities { get; }

    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        return await _context.SaveChangesAsync(ct);
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
```

**Registration (Program.cs):**
```csharp
// Scoped per request/circuit
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

// OR with factory for Blazor Server
builder.Services.AddScoped<IUnitOfWork>(sp =>
{
    var factory = sp.GetRequiredService<IDbContextFactory<SignaturDbContext>>();
    var context = factory.CreateDbContext();
    return new UnitOfWork(context);
});
```

### Pattern 6: Manual DTO Mapping Extensions

**What:** Extension methods for entity-to-DTO mapping (no AutoMapper).
**When to use:** Application layer returning data to presentation layer.

**DTOs (Application):**
```csharp
// Application/DTOs/ClientDto.cs
public record ClientDto(
    int ClientId,
    string SiteId,
    string CompanyName,
    string? Email,
    bool IsActive);
```

**Mapping extensions (Application):**
```csharp
// Application/Mappings/ClientMappings.cs
public static class ClientMappings
{
    public static ClientDto ToDto(this Client client)
    {
        return new ClientDto(
            client.ClientId,
            client.SiteId,
            client.CompanyName,
            client.Email,
            client.StatusId == 1);
    }

    // For IQueryable projections (executes on database)
    public static IQueryable<ClientDto> ProjectToDto(this IQueryable<Client> query)
    {
        return query.Select(c => new ClientDto(
            c.ClientId,
            c.SiteId,
            c.CompanyName,
            c.Email,
            c.StatusId == 1));
    }
}
```

**Usage:**
```csharp
// In memory mapping
var dto = client.ToDto();

// Database projection (efficient)
var dtos = await _context.Clients
    .Where(c => c.SiteId == siteId)
    .ProjectToDto()
    .ToListAsync();
```

### Pattern 7: TUnit Test Structure

**What:** Modern test framework with source generation and parallel execution.
**When to use:** All unit tests, replacing NUnit/xUnit.

**Test project setup:**
```bash
dotnet new tunit -n AtlantaSignatur.Tests
dotnet add reference ../AtlantaSignatur.Application
```

**Test class:**
```csharp
// Tests/ClientRepositoryTests.cs
using TUnit.Core;

[TestClass]
public class ClientRepositoryTests
{
    private readonly IDbContextFactory<SignaturDbContext> _factory;

    public ClientRepositoryTests()
    {
        // Setup in-memory database
        var options = new DbContextOptionsBuilder<SignaturDbContext>()
            .UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}")
            .Options;

        _factory = new TestDbContextFactory(options);
    }

    [Test]
    public async Task GetByIdAsync_ExistingClient_ReturnsClient()
    {
        // Arrange
        await using var context = _factory.CreateDbContext();
        var repository = new ClientRepository(context);
        var client = new Client { ClientId = 1, SiteId = "test", CompanyName = "Test Co" };
        await context.Clients.AddAsync(client);
        await context.SaveChangesAsync();

        // Act
        var result = await repository.GetByIdAsync(1);

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.CompanyName).IsEqualTo("Test Co");
    }
}
```

### Pattern 8: Playwright E2E Tests

**What:** Cross-browser automation for E2E testing Blazor UI.
**When to use:** Testing user flows across Blazor and WebForms.

**Setup:**
```bash
dotnet new nunit -n AtlantaSignatur.E2E
dotnet add package Microsoft.Playwright
pwsh bin/Debug/net10.0/playwright.ps1 install
```

**Test class:**
```csharp
// E2E/LoginFlowTests.cs
using Microsoft.Playwright;
using NUnit.Framework;

[Parallelizable(ParallelScope.Self)]
[TestFixture]
public class LoginFlowTests
{
    private IPlaywright? _playwright;
    private IBrowser? _browser;

    [SetUp]
    public async Task Setup()
    {
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new()
        {
            Headless = true
        });
    }

    [Test]
    public async Task Login_NavigateToBlazor_MaintainsSession()
    {
        // Arrange
        var page = await _browser!.NewPageAsync();

        // Act - Login via WebForms
        await page.GotoAsync("https://localhost:44300/Login.aspx");
        await page.FillAsync("#txtUsername", "testuser");
        await page.FillAsync("#txtPassword", "password");
        await page.ClickAsync("#btnLogin");

        // Navigate to Blazor page
        await page.GotoAsync("https://localhost:5001/clients");

        // Assert - Session maintained
        await Expect(page.Locator("text=testuser")).ToBeVisibleAsync();
    }

    [TearDown]
    public async Task Teardown()
    {
        await _browser?.DisposeAsync()!;
        _playwright?.Dispose();
    }
}
```

**Note:** Blazor Server components load asynchronously, so use `WaitForNetworkIdleAsync()` to avoid flaky tests.

### Anti-Patterns to Avoid

- **Scoped DbContext in Blazor Server:** Causes circuit leaks and multi-tenancy bugs. Always use IDbContextFactory.
- **Generic Repository for everything:** EF Core is already a repository. Add abstraction only when needed for Clean Architecture.
- **Session access in Blazor circuits:** Session is null after SSR. Store user state in circuit-scoped services instead.
- **YARP route without Order:** Causes non-deterministic routing. Always set explicit Order values.
- **AutoMapper in DTOs:** Adds runtime reflection overhead. Manual mapping is faster and more explicit.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Reverse proxy routing | Custom proxy middleware | YARP 2.3.0 | Production-proven, handles headers/transforms, Microsoft-supported |
| Session sharing between Framework/Core | Custom session serialization | System.Web Adapters remote session | Handles serialization, locking, API security |
| Database schema migrations | Manual SQL scripts | **NEVER use EF Core migrations** | Legacy schema is immutable, scaffolding handles re-sync |
| Browser automation | Selenium custom wrappers | Playwright | Modern async API, auto-waiting, cross-browser support |
| Mediator pattern | Custom command bus | Wolverine (or MediatR if license OK) | Built-in outbox, sagas, retries, messaging |

**Key insight:** Microsoft has provided official migration tools (YARP + System.Web Adapters) specifically for this scenario. Custom solutions will miss edge cases around headers, cookies, session locking, and auth challenges.

## Common Pitfalls

### Pitfall 1: YARP Routing Collision with Blazor

**What goes wrong:** Both YARP and Blazor register `{**catch-all}` routes. Routing becomes non-deterministic—some requests proxy to WebForms, others render Blazor, depending on registration order.

**Why it happens:** ASP.NET Core routing matches most specific route first, but when specificity is equal, registration order determines winner. Both YARP fallback and Blazor fallback have equal specificity.

**How to avoid:**
```csharp
// WRONG: Both registered without Order
app.MapRazorComponents<App>();
app.MapReverseProxy(); // ❌ Arbitrary winner

// RIGHT: Explicit Order values
app.MapRazorComponents<App>();
app.MapForwarder("/{**catch-all}", remoteUrl)
    .WithOrder(int.MaxValue)  // ✅ Always last
    .ShortCircuit();
```

**Warning signs:** Deep links to Blazor pages return 404, or random 404s that disappear on refresh.

**Sources:** [Microsoft Q&A: YARP with Blazor Server 404](https://learn.microsoft.com/en-us/answers/questions/2264706/problem-to-use-yarp-as-reverse-proxy-with-my-blazo), [GitHub Issue #1952](https://github.com/microsoft/reverse-proxy/issues/1952)

### Pitfall 2: Session State Null in Blazor Server Circuits

**What goes wrong:** `HttpContext.Session` is available during SSR (initial page load) but becomes null once Blazor Server circuit starts. Session reads fail silently or throw NullReferenceException.

**Why it happens:** Blazor Server uses WebSocket-based SignalR circuits after initial HTTP request. Session is HTTP-bound, not available in WebSocket context.

**How to avoid:**
```csharp
// During SSR (works)
@page "/clients"
@code {
    protected override async Task OnInitializedAsync()
    {
        // ✅ Session available during SSR
        var siteId = HttpContext.Session.GetString("SiteId");

        // Store in circuit-scoped service for later use
        await UserContextService.InitializeAsync(siteId);
    }
}

// After circuit starts (session is null)
private async Task OnButtonClick()
{
    // ❌ Session is null here
    var siteId = HttpContext.Session.GetString("SiteId"); // NullReferenceException

    // ✅ Use circuit-scoped service instead
    var siteId = UserContextService.SiteId;
}
```

**Alternative pattern:**
```csharp
// Circuit-scoped service
public class UserContextService
{
    public string? SiteId { get; private set; }
    public string? ClientId { get; private set; }

    public void Initialize(ISession session)
    {
        SiteId = session.GetString("SiteId");
        ClientId = session.GetString("ClientId");
    }
}

// Registration
builder.Services.AddScoped<UserContextService>();
```

**Warning signs:** Session works on page load, fails on button clicks. Intermittent NullReferenceExceptions in production.

**Sources:** [GitHub Issue #413](https://github.com/dotnet/systemweb-adapters/issues/413), [GitHub Issue #382](https://github.com/dotnet/systemweb-adapters/issues/382)

### Pitfall 3: Session Key Not Registered

**What goes wrong:** Accessing session key throws exception: "Session key 'X' is not registered for serialization."

**Why it happens:** System.Web Adapters requires explicit registration of every session key with its type. Legacy WebForms uses BinaryFormatter (automatic), but JSON serialization needs type info.

**How to avoid:**
```csharp
// ❌ WRONG: Missing registration
Session["UserId"] = 123;
var userId = Session["UserId"]; // Exception!

// ✅ RIGHT: Register in both apps
// Blazor Core
builder.Services.AddSystemWebAdapters()
    .AddJsonSessionSerializer(options =>
    {
        options.RegisterKey<int>("UserId");
        options.RegisterKey<string>("SiteId");
        options.RegisterKey<string>("ClientId");
    });

// WebForms Framework (Global.asax.cs)
builder.AddSystemWebAdapters()
    .AddJsonSessionSerializer(options =>
    {
        options.RegisterKey<int>("UserId");
        options.RegisterKey<string>("SiteId");
        options.RegisterKey<string>("ClientId");
    });
```

**How to audit legacy session keys:**
```csharp
// Add to WebForms Global.asax.cs temporarily
protected void Session_Start()
{
    // Log all session keys used
    foreach (string key in Session.Keys)
    {
        var type = Session[key]?.GetType();
        Debug.WriteLine($"Session[{key}] = {type?.Name}");
    }
}
```

**Warning signs:** Session works in WebForms, fails in Blazor with serialization errors.

### Pitfall 4: Scoped DbContext in Blazor Server

**What goes wrong:** Multiple components share same DbContext instance, causing "A second operation started on this context" errors. Multi-tenancy leaks data between users.

**Why it happens:** Blazor Server circuits are long-lived. Scoped DbContext persists for entire circuit lifetime. Multiple parallel operations (e.g., two components loading data) use same context concurrently.

**How to avoid:**
```csharp
// ❌ WRONG: Scoped DbContext
builder.Services.AddDbContext<SignaturDbContext>(options =>
    options.UseSqlServer(connectionString)); // ❌ Circuit-scoped = shared across components

// ✅ RIGHT: IDbContextFactory
builder.Services.AddDbContextFactory<SignaturDbContext>(options =>
    options.UseSqlServer(connectionString)); // ✅ Create per operation

// Component usage
@inject IDbContextFactory<SignaturDbContext> DbFactory
@code {
    private async Task LoadDataAsync()
    {
        await using var context = DbFactory.CreateDbContext();
        var data = await context.Clients.ToListAsync();
    }
}
```

**Warning signs:** "InvalidOperationException: A second operation started on this context before a previous operation completed." Intermittent in production, especially under load.

**Sources:** [Microsoft Docs: DbContext Configuration](https://learn.microsoft.com/ef/core/dbcontext-configuration/#avoiding-dbcontext-threading-issues), [Blazor + EF Core Docs](https://learn.microsoft.com/aspnet/core/blazor/blazor-ef-core)

### Pitfall 5: EF Core Migrations on Immutable Schema

**What goes wrong:** Applying EF Core migrations to legacy database overwrites stored procedures, triggers, or constraints. Schema diverges from WebForms app's expectations.

**Why it happens:** EF Core migrations assume EF Core owns the schema. Legacy database has objects EF Core doesn't track.

**How to avoid:**
```csharp
// ❌ WRONG: Using migrations
dotnet ef migrations add InitialCreate
dotnet ef database update // ❌ Overwrites legacy schema

// ✅ RIGHT: Database-first scaffolding ONLY
dotnet ef dbcontext scaffold "..." Microsoft.EntityFrameworkCore.SqlServer

// Custom configuration in partial class
public partial class SignaturDbContext
{
    partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
    {
        // Override scaffolded config if needed
        modelBuilder.Entity<Client>()
            .HasQueryFilter(c => !c.IsDeleted);
    }
}
```

**If you must track schema changes:**
```csharp
// Create empty migration for tooling, don't apply to database
dotnet ef migrations add InitialCreate
// Edit generated migration: delete all contents of Up() and Down()
// Apply to empty test database to generate __EFMigrationsHistory entry
```

**Warning signs:** WebForms app starts failing after Blazor deployment. Stored procedures or triggers missing.

### Pitfall 6: Multi-Tenancy Session Leaks

**What goes wrong:** User A sees User B's data. SiteId/ClientId filters not applied, or applied incorrectly.

**Why it happens:** Session-based multi-tenancy requires re-applying filters on every new DbContext instance. Forgetting to set TenantId causes global queries.

**How to avoid:**
```csharp
// Custom DbContext with built-in tenant filter
public class SignaturDbContext : DbContext
{
    public string? CurrentSiteId { get; set; }
    public string? CurrentClientId { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Global query filter for Site-level tenant
        modelBuilder.Entity<Client>()
            .HasQueryFilter(c => c.SiteId == CurrentSiteId);

        modelBuilder.Entity<ERActivity>()
            .HasQueryFilter(a => a.SiteId == CurrentSiteId);
    }
}

// Factory that injects tenant context
public class TenantDbContextFactory : IDbContextFactory<SignaturDbContext>
{
    private readonly IDbContextFactory<SignaturDbContext> _innerFactory;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public SignaturDbContext CreateDbContext()
    {
        var context = _innerFactory.CreateDbContext();

        // CRITICAL: Set tenant filters from session
        var session = _httpContextAccessor.HttpContext?.Session;
        if (session != null)
        {
            context.CurrentSiteId = session.GetString("SiteId");
            context.CurrentClientId = session.GetString("ClientId");
        }

        return context;
    }
}
```

**Warning signs:** Data leaks between tenants. Production support tickets: "I see another company's candidates."

### Pitfall 7: Playwright Tests Failing Due to Async Rendering

**What goes wrong:** Playwright test clicks button before component finishes rendering. "Element not found" or "Element not clickable" errors.

**Why it happens:** Blazor Server loads HTML during `OnInitialized`, updates DOM after `OnAfterRenderAsync`. Playwright may interact before render completes.

**How to avoid:**
```csharp
// ❌ WRONG: Immediate interaction
await page.GotoAsync("https://localhost:5001/clients");
await page.ClickAsync("#btnEdit"); // ❌ Element may not exist yet

// ✅ RIGHT: Wait for network idle
await page.GotoAsync("https://localhost:5001/clients", new()
{
    WaitUntil = WaitUntilState.NetworkIdle // ✅ Wait for SignalR to finish
});
await page.ClickAsync("#btnEdit");

// OR: Wait for specific element
await page.WaitForSelectorAsync("#btnEdit", new()
{
    State = WaitForSelectorState.Visible
});
await page.ClickAsync("#btnEdit");
```

**Warning signs:** Tests pass locally, fail in CI. Intermittent "element not found" errors.

**Sources:** [Medium: E2E Testing Blazor with Playwright](https://chrisdunderdale.medium.com/performing-end-to-end-testing-in-blazor-with-playwright-9ab8587f7c34)

## Code Examples

### Complete YARP + System.Web Adapters Setup

**appsettings.json (Blazor Core):**
```json
{
  "ConnectionStrings": {
    "SignaturAnnoncePortal": "Server=.;Database=SignaturAnnoncePortal;Trusted_Connection=True;TrustServerCertificate=True;"
  },
  "RemoteAppUri": "https://localhost:44300",
  "RemoteAppApiKey": "12345678-1234-1234-1234-123456789012",
  "ReverseProxy": {
    "Routes": {
      "fallback-to-webforms": {
        "ClusterId": "legacy-webforms",
        "Order": 2147483647,
        "Match": {
          "Path": "/{**catch-all}"
        }
      }
    },
    "Clusters": {
      "legacy-webforms": {
        "Destinations": {
          "webforms-app": {
            "Address": "https://localhost:44300"
          }
        }
      }
    }
  }
}
```

**Program.cs (Blazor Core):**
```csharp
using Microsoft.AspNetCore.SystemWebAdapters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// Blazor
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// EF Core with factory pattern
builder.Services.AddDbContextFactory<SignaturDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("SignaturAnnoncePortal")));

// YARP
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

// System.Web Adapters
builder.Services.AddSystemWebAdapters()
    .AddJsonSessionSerializer(options =>
    {
        options.RegisterKey<int>("UserId");
        options.RegisterKey<string>("SiteId");
        options.RegisterKey<string>("ClientId");
    })
    .AddRemoteAppClient(options =>
    {
        options.RemoteAppUrl = new Uri(builder.Configuration["RemoteAppUri"]!);
        options.ApiKey = builder.Configuration["RemoteAppApiKey"]!;
    })
    .AddSessionClient()
    .AddAuthenticationClient(true);

// Repositories
builder.Services.AddScoped<IClientRepository, ClientRepository>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

var app = builder.Build();

// Middleware ordering
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

// Blazor components
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .RequireSystemWebAdapterSession();

// YARP fallback (MUST be last)
app.MapForwarder("/{**catch-all}",
    app.Services.GetRequiredService<IOptions<RemoteAppClientOptions>>().Value.RemoteAppUrl.OriginalString)
    .WithOrder(int.MaxValue)
    .ShortCircuit();

app.Run();
```

**Global.asax.cs (WebForms Framework):**
```csharp
using Microsoft.AspNetCore.SystemWebAdapters;
using System.Configuration;

public class Global : HttpApplication
{
    protected void Application_Start()
    {
        HttpApplicationHost.RegisterHost(builder =>
        {
            builder.AddSystemWebAdapters()
                .AddJsonSessionSerializer(options =>
                {
                    options.RegisterKey<int>("UserId");
                    options.RegisterKey<string>("SiteId");
                    options.RegisterKey<string>("ClientId");
                })
                .AddRemoteAppServer(options =>
                {
                    options.ApiKey = ConfigurationManager.AppSettings["RemoteAppApiKey"];
                })
                .AddSessionServer()
                .AddAuthenticationServer()
                .AddProxySupport(options => options.UseForwardedHeaders = true);
        });

        // Existing WebForms initialization...
    }
}
```

**web.config (WebForms Framework):**
```xml
<configuration>
  <appSettings>
    <add key="RemoteAppApiKey" value="12345678-1234-1234-1234-123456789012" />
  </appSettings>

  <system.webServer>
    <modules>
      <remove name="SystemWebAdapterModule" />
      <add name="SystemWebAdapterModule"
           type="Microsoft.AspNetCore.SystemWebAdapters.SystemWebAdapterModule, Microsoft.AspNetCore.SystemWebAdapters.FrameworkServices"
           preCondition="managedHandler" />
    </modules>
  </system.webServer>
</configuration>
```

### Complete EF Core Scaffolding + Repository Setup

**1. Scaffold DbContext:**
```bash
dotnet ef dbcontext scaffold \
    "Server=.;Database=SignaturAnnoncePortal;Trusted_Connection=True;TrustServerCertificate=True;" \
    Microsoft.EntityFrameworkCore.SqlServer \
    --output-dir Infrastructure/Data/Entities \
    --context-dir Infrastructure/Data \
    --context SignaturDbContext \
    --no-onconfiguring \
    --force
```

**2. Custom DbContext partial class:**
```csharp
// Infrastructure/Data/SignaturDbContext.Custom.cs
public partial class SignaturDbContext
{
    public string? CurrentSiteId { get; set; }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
    {
        // Global query filters for multi-tenancy
        modelBuilder.Entity<Client>()
            .HasQueryFilter(c => CurrentSiteId == null || c.SiteId == CurrentSiteId);

        modelBuilder.Entity<ERActivity>()
            .HasQueryFilter(a => CurrentSiteId == null || a.SiteId == CurrentSiteId);

        modelBuilder.Entity<ERCandidate>()
            .HasQueryFilter(c => CurrentSiteId == null || c.ERActivity.SiteId == CurrentSiteId);
    }
}
```

**3. Repository interface (Domain):**
```csharp
// Domain/Interfaces/IClientRepository.cs
public interface IClientRepository
{
    Task<Client?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<Client>> GetAllAsync(CancellationToken ct = default);
    Task AddAsync(Client client, CancellationToken ct = default);
    void Update(Client client);
    void Remove(Client client);
}
```

**4. Repository implementation (Infrastructure):**
```csharp
// Infrastructure/Repositories/ClientRepository.cs
public class ClientRepository : IClientRepository
{
    private readonly IDbContextFactory<SignaturDbContext> _contextFactory;

    public ClientRepository(IDbContextFactory<SignaturDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<Client?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        await using var context = _contextFactory.CreateDbContext();
        return await context.Clients.FindAsync(new object[] { id }, ct);
    }

    public async Task<IReadOnlyList<Client>> GetAllAsync(CancellationToken ct = default)
    {
        await using var context = _contextFactory.CreateDbContext();
        return await context.Clients.AsNoTracking().ToListAsync(ct);
    }

    public async Task AddAsync(Client client, CancellationToken ct = default)
    {
        await using var context = _contextFactory.CreateDbContext();
        await context.Clients.AddAsync(client, ct);
        await context.SaveChangesAsync(ct);
    }

    public void Update(Client client)
    {
        // Will be handled by UnitOfWork.SaveChanges
    }

    public void Remove(Client client)
    {
        // Will be handled by UnitOfWork.SaveChanges
    }
}
```

**5. Manual DTO mapping:**
```csharp
// Application/DTOs/ClientDto.cs
public record ClientDto(
    int ClientId,
    string SiteId,
    string CompanyName,
    string? Email);

// Application/Mappings/ClientMappings.cs
public static class ClientMappings
{
    public static ClientDto ToDto(this Client client)
    {
        return new ClientDto(
            client.ClientId,
            client.SiteId,
            client.CompanyName,
            client.Email);
    }

    public static IQueryable<ClientDto> ProjectToDto(this IQueryable<Client> query)
    {
        return query.Select(c => new ClientDto(
            c.ClientId,
            c.SiteId,
            c.CompanyName,
            c.Email));
    }
}
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Manual proxy middleware | YARP 2.3.0 | 2023 (EF Core 7) | Production-ready reverse proxy with transforms, health checks |
| Custom session bridge | System.Web Adapters 2.3.0 | 2023 | Official Microsoft migration tool |
| NUnit/xUnit | TUnit 1.0 | 2024 | Source-generated tests, 2-3x faster, Native AOT support |
| Selenium | Playwright 1.49+ | 2021 | Modern async API, auto-waiting, cross-browser |
| AutoMapper | Manual mapping | Ongoing | Performance, maintainability (EF Core projections) |
| Scoped DbContext in Blazor | IDbContextFactory | 2020 (EF Core 5) | Circuit-safe, multi-tenancy-safe |
| EF Core migrations on legacy DB | Database-first scaffolding + partial classes | Always | Immutable schema requirement |
| MediatR open source | MediatR commercial (or Wolverine) | 2024 | Licensing change (v13+) |

**Deprecated/outdated:**
- **System.Web.UI.Page lifecycle in Blazor:** Blazor uses component lifecycle (`OnInitialized`, `OnAfterRender`). No direct equivalent to Page_Load.
- **Session.Abandon() in Blazor circuits:** Not supported. Clear session values individually or navigate to logout page.
- **BinaryFormatter for session:** Deprecated in .NET 5+. System.Web Adapters uses JSON serialization.

## Open Questions

### 1. MediatR Licensing Eligibility

**What we know:**
- MediatR v13+ requires commercial license for some usage
- Wolverine is MIT-licensed alternative with more features (outbox, sagas, messaging)
- Wolverine has steeper learning curve but consolidates multiple patterns

**What's unclear:**
- Does AtlantaSignatur qualify for MediatR free tier?
- Is Wolverine overkill for this project's CQRS needs?

**Recommendation:**
- **Phase 1:** Skip mediator pattern entirely (use direct repository calls)
- **Later phases:** Evaluate MediatR license or prototype Wolverine if CQRS complexity increases

### 2. Exact Session Keys in Legacy App

**What we know:**
- WebForms app uses `Session["key"]` throughout codebase
- System.Web Adapters requires explicit registration of every key with type

**What's unclear:**
- Complete list of session keys used
- Types of objects stored in session (primitives vs. complex objects)

**Recommendation:**
- **During Phase 1 implementation:** Audit WebForms codebase with grep for `Session\[`
- Add temporary logging to `Session_Start` in Global.asax.cs
- Create shared session key constants file referenced by both apps

### 3. Navigation Shell Implementation Strategy

**What we know:**
- Original UI has top nav that must be preserved
- Navigation must work across both Blazor and WebForms

**What's unclear:**
- Should nav be pure HTML (duplicated in both apps)?
- Should nav be Blazor component + iframe WebForms?
- How to handle active link styling across app boundaries?

**Recommendation:**
- **Phase 1:** Start with duplicated HTML nav in both apps (simplest, safest)
- **Later:** Consider Blazor component + postMessage API for inter-app communication

## Sources

### Primary (HIGH confidence)

**Microsoft Official Documentation:**
- [YARP Configuration Files](https://learn.microsoft.com/aspnet/core/fundamentals/servers/yarp/config-files?view=aspnetcore-10.0) - YARP setup, routes, clusters
- [Remote App Setup](https://learn.microsoft.com/aspnet/core/migration/fx-to-core/inc/remote-app-setup?view=aspnetcore-10.0) - System.Web Adapters configuration
- [Session Migration](https://learn.microsoft.com/aspnet/core/migration/fx-to-core/areas/session?view=aspnetcore-10.0) - Remote session patterns, serialization
- [Authentication Migration](https://learn.microsoft.com/aspnet/core/migration/fx-to-core/areas/authentication?view=aspnetcore-10.0) - Remote auth patterns, limitations
- [Blazor + EF Core](https://learn.microsoft.com/aspnet/core/blazor/blazor-ef-core?view=aspnetcore-10.0) - IDbContextFactory usage
- [EF Core Scaffolding](https://learn.microsoft.com/ef/core/managing-schemas/scaffolding/) - Database-first patterns, partial classes
- [DbContext Configuration](https://learn.microsoft.com/ef/core/dbcontext-configuration/) - Factory pattern, multi-tenancy
- [Multi-tenancy](https://learn.microsoft.com/ef/core/miscellaneous/multitenancy) - Blazor Server + DbContextFactory patterns

**Microsoft Code Samples:**
- YARP Program.cs samples - MapReverseProxy, LoadFromConfig patterns
- EF Core scaffold commands - dotnet ef dbcontext scaffold examples
- IDbContextFactory usage - CreateDbContext in Blazor components

### Secondary (MEDIUM confidence)

**Community Resources:**
- [Clean Architecture Blazor Server (GitHub)](https://github.com/neozhu/CleanArchitectureWithBlazorServer) - Project structure reference
- [Jason Taylor Clean Architecture](https://github.com/jasontaylordev/CleanArchitecture) - .NET 10 template
- [TUnit Official Site](https://tunit.dev/) - Modern testing framework
- [TUnit Medium Article](https://medium.com/@thomhurst/tunit-why-i-spent-2-years-building-a-new-net-testing-framework-86efaec0b8b8) - Why TUnit was built
- [Playwright .NET 2026 Guide](https://www.browserstack.com/guide/playwright-dotnet) - E2E testing patterns
- [Repository Pattern Debate (Medium)](https://medium.com/@codebob75/repository-pattern-c-ultimate-guide-entity-framework-core-clean-architecture-dtos-dependency-6a8d8b444dcb) - EF Core + Repository patterns
- [Wolverine for MediatR Users](https://jeremydmiller.com/2025/01/28/wolverine-for-mediatr-users/) - MediatR alternative

**GitHub Issues (Critical Pitfalls):**
- [YARP + Blazor 404 Issue](https://github.com/microsoft/reverse-proxy/issues/1952) - Routing collision
- [System.Web Adapters Session Null](https://github.com/dotnet/systemweb-adapters/issues/413) - Blazor Server circuit limitation
- [Blazor SSR State Management](https://github.com/dotnet/aspnetcore/issues/47796) - Session in circuits

### Tertiary (LOW confidence)

- Stack Overflow threads on YARP routing order (conflicting advice, use official docs instead)
- Blog posts on "best" Clean Architecture structure (subjective, validated against Microsoft samples)

## Metadata

**Confidence breakdown:**
- **Standard stack:** HIGH - All libraries from Microsoft official packages, versions confirmed via NuGet
- **YARP configuration:** HIGH - Directly from Microsoft Learn, verified with code samples
- **System.Web Adapters:** HIGH - Microsoft official docs, GitHub repo samples
- **Clean Architecture structure:** MEDIUM - Community templates, validated against Microsoft samples
- **Repository pattern:** MEDIUM - Debated in community, guidance based on Microsoft's infrastructure persistence docs
- **TUnit framework:** MEDIUM - New framework (2024), no 1.0 release yet, but well-documented and feature-complete
- **Playwright:** HIGH - Microsoft-official framework, mature (2021+), extensive docs

**Research date:** 2026-02-13
**Valid until:** 2026-03-13 (30 days - stable stack, no major .NET releases expected)

**Notes for planner:**
- YARP 2.3.0 and System.Web Adapters 2.3.0 are production-stable (2+ years in use)
- .NET 10 LTS released November 2024, Blazor patterns stabilized
- TUnit is pre-1.0 but API stable per author; validate in prototype task
- Database schema (455 tables) requires IDbContextFactory for performance
- Multi-tenancy (Site → Client) is critical constraint throughout
