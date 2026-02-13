# Stack Research

**Domain:** ASP.NET WebForms to Blazor incremental migration (ATS/E-recruitment platform)
**Project:** AtlantaSignatur Modernization
**Researched:** 2026-02-13
**Confidence:** HIGH

---

## Executive Summary

This stack is designed for incremental migration of a production ASP.NET WebForms (.NET Framework 4.8) application to .NET 10 + Blazor, using YARP reverse proxy for zero-downtime cutover and System.Web Adapters for session/auth sharing during the transition period. Every technology choice is driven by three constraints: (1) the existing database schema cannot be altered, (2) users must experience zero downtime, and (3) the new system must be maintainable for 10+ years.

.NET 10 (LTS, released November 11, 2025, supported through November 2028) is the correct target. It is the current Long-Term Support release, giving a three-year support window and aligning with the project's long-term maintainability requirement.

---

## Recommended Stack

### Core Framework

| Technology | Version | Purpose | Why Recommended | Confidence |
|------------|---------|---------|-----------------|------------|
| .NET 10 SDK | 10.0.x (latest patch: 10.0.3, Feb 2026) | Runtime and SDK | LTS release (Nov 2025 - Nov 2028). Three-year support window. Required for all downstream libraries targeting net10.0. Shipped with C# 14 (field-backed properties, improved `nameof`). | HIGH |
| ASP.NET Core 10 | 10.0.x | Web framework | Ships with .NET 10. Includes built-in validation for Minimal APIs, OpenAPI 3.1 support, passkey authentication, and SSE support. Foundation for both Blazor and YARP. | HIGH |
| Blazor (Interactive Server) | Ships with .NET 10 | UI framework | Server-side rendering with SignalR interactivity. Best fit for this migration because: (a) no WASM download penalty for users, (b) full server-side data access for existing database, (c) prerendering support for fast initial loads, (d) new .NET 10 `[PersistentState]` attribute solves prerender flicker. | HIGH |
| C# 14 | Ships with .NET 10 | Language | Field-backed properties reduce boilerplate. `nameof` with unbound generics. Incremental improvements over C# 13. | HIGH |

**Blazor Render Mode Decision: Interactive Server (not WASM, not Auto)**

Use `InteractiveServer` render mode because:
- The existing WebForms app is server-rendered. Interactive Server preserves the same architectural model.
- Direct database access from server-side Blazor components avoids the API layer WASM would require.
- No WebAssembly download latency. The ATS portal needs fast first-load for recruiters.
- YARP fallback routing works naturally with server-side rendering.
- The new .NET 10 `[PersistentState]` attribute and `ReconnectModal` component address the two historical weaknesses of Blazor Server (prerender state loss and disconnect handling).

### Migration Infrastructure

| Technology | Version | Purpose | Why Recommended | Confidence |
|------------|---------|---------|-----------------|------------|
| YARP (Yarp.ReverseProxy) | 2.3.0 | Reverse proxy for incremental migration | Microsoft-maintained. Supports .NET 8+, fully compatible with .NET 10. Enables the core migration pattern: ASP.NET Core app sits in front, serves migrated pages directly, proxies unmigrated pages to the legacy WebForms app. Fallback routing via `MapForwarder` or `MapReverseProxy`. | HIGH |
| System.Web Adapters | 2.3.0 (Microsoft.AspNetCore.SystemWebAdapters) | Session and auth sharing | Microsoft's official bridge for incremental migration. Provides familiar System.Web types (`HttpContext`, `HttpRequest`, etc.) backed by ASP.NET Core implementations. Critical for sharing session state and authentication between old and new apps during the transition. | HIGH |

**YARP Configuration Pattern for This Project:**

```
User Request
    |
    v
[ASP.NET Core 10 + Blazor App] (YARP host)
    |
    +--> Migrated route? --> Serve via Blazor component
    |
    +--> Not migrated? --> Forward to legacy WebForms app via YARP
```

Key YARP setup:
```csharp
// Program.cs in the new ASP.NET Core app
builder.Services.AddSystemWebAdapters()
    .AddRemoteAppClient(options =>
    {
        options.RemoteAppUrl = new(builder.Configuration["RemoteAppUri"]);
        options.ApiKey = builder.Configuration["RemoteAppApiKey"];
    })
    .AddAuthenticationClient(true)   // Share auth with legacy app
    .AddRemoteAppSession();          // Share session with legacy app

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

// Migrated Blazor routes take precedence
app.MapBlazorPages();  // or app.MapRazorComponents<App>()

// Unmigrated routes fall through to YARP
app.MapReverseProxy();
// OR use direct forwarding with short-circuit:
// app.MapForwarder("/{**catch-all}", legacyAppUrl, ...).ShortCircuit();
```

**System.Web Adapters Session Sharing Pattern:**

Three session strategies available (use Remote App Session for this project):

| Approach | Code Changes | Performance | Session Sharing | When to Use |
|----------|-------------|-------------|-----------------|-------------|
| Built-in ASP.NET Core | High | Best | None | Complete rewrites |
| Wrapped ASP.NET Core | Low | Good | None | Incremental, no shared state |
| **Remote App** | **Low** | **Fair** | **Full** | **Running both apps simultaneously** |

Use Remote App Session because both apps must share session data during the transition period. Register session key types explicitly:

```csharp
builder.Services.AddSystemWebAdapters()
    .AddJsonSessionSerializer(options =>
    {
        options.RegisterKey<int>("UserId");
        options.RegisterKey<string>("UserName");
        // Register all session keys used by the legacy app
    })
    .AddRemoteAppSession();
```

### Database & Data Access

| Technology | Version | Purpose | Why Recommended | Confidence |
|------------|---------|---------|-----------------|------------|
| EF Core 10 | 10.0.x | ORM / Data access | Ships with .NET 10 LTS. Database-first scaffolding (`dotnet ef dbcontext scaffold`) generates entities and DbContext from existing schema. Supports partial classes so scaffolded code can be extended without modification. New LINQ `LeftJoin`/`RightJoin` operators. Named query filters. | HIGH |
| Microsoft.EntityFrameworkCore.SqlServer | 10.0.x | SQL Server provider | Matches the existing SQL Server database. Supports vector types and JSON columns (SQL Server 2025). | HIGH |
| Microsoft.EntityFrameworkCore.Design | 10.0.x | Scaffolding tooling | Required for `dotnet ef dbcontext scaffold` to reverse-engineer existing schema. | HIGH |
| EF Core Power Tools | Latest (supports EF Core 8-10) | Visual scaffolding | Optional but recommended. VS extension that provides GUI-driven scaffolding with more control than CLI. Generates cleaner code with configurable T4 templates. | MEDIUM |

**Database-First Strategy for Immutable Schema:**

Since the database schema cannot be altered, use scaffold-once with partial classes:

1. **Initial scaffold**: `dotnet ef dbcontext scaffold "ConnectionString" Microsoft.EntityFrameworkCore.SqlServer --output-dir Models --context-dir Data --context AppDbContext --force`
2. **Partial classes**: Both DbContext and entity classes are generated as `partial`. Add custom logic (computed properties, domain methods) in separate partial class files that survive re-scaffolding.
3. **OnModelCreatingPartial**: Override scaffolded configuration without editing generated files:
   ```csharp
   public partial class AppDbContext
   {
       partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
       {
           // Custom configuration that overrides scaffolded defaults
       }
   }
   ```
4. **T4 Template Customization** (EF7+): Customize code generation templates for consistent output across re-scaffolds.
5. **No migrations**: Never use `Add-Migration`. The database is the source of truth. Use scaffolding only.

### UI Components

| Technology | Version | Purpose | Why Recommended | Confidence |
|------------|---------|---------|-----------------|------------|
| MudBlazor | 8.15.0 (stable) / 9.0.0-preview.1 (with .NET 10 target) | Component library | Material Design-based. Most popular open-source Blazor component library. Rich DataGrid, Forms, Dialog, Navigation components. Active community. Free and MIT-licensed. | HIGH |

**MudBlazor Version Strategy:**

- **Start with 8.15.0** (current stable, supports .NET 8 and .NET 9). Works on .NET 10 with minor issues in only 3 components.
- **Upgrade to 9.x when stable** (preview available as 9.0.0-preview.1, targets .NET 8/9/10 officially). Monitor [MudBlazor releases](https://github.com/MudBlazor/MudBlazor/releases) for stable 9.0 release.
- The Activity List View (core value) will use `MudDataGrid` for data display with sorting, filtering, and pagination. MudBlazor's DataGrid is production-ready and handles the table-heavy UI patterns common in ATS/recruitment platforms.

### Architecture Libraries

| Library | Version | Purpose | Why Recommended | Confidence |
|---------|---------|---------|-----------------|------------|
| MediatR | 14.0.0 | CQRS / Mediator pattern | Industry standard for command/query separation in .NET. Enables clean separation between Blazor components and business logic. Pipeline behaviors for cross-cutting concerns (validation, logging, caching). | MEDIUM |
| FluentValidation | 12.1.1 | Input validation | Strongly-typed validation rules. Integrates with MediatR pipeline behaviors. Supports .NET 8+. Better than Data Annotations for complex business rules. | HIGH |
| Mapperly (Riok.Mapperly) | 4.3.1 | Object mapping (DTO <-> Entity) | Source-generator based. Zero runtime reflection overhead. Compile-time error detection. Faster and more memory-efficient than AutoMapper. Free and actively maintained. The 2025-2026 standard choice for new .NET projects. | HIGH |

**MediatR Licensing Note (MEDIUM confidence on continued use):**

MediatR v13+ adopted a dual-license model (RPL-1.5 + commercial). A free Community Edition exists for companies under $5M revenue, non-profits, and educational use. Verify AtlantaSignatur's eligibility. If licensing is a concern, **Wolverine** (WolverineFx 5.x, MIT license) is the recommended alternative -- it uses source generators for better performance and includes built-in EF Core integration via `WolverineFx.EntityFrameworkCore`.

### Logging & Observability

| Library | Version | Purpose | Why Recommended | Confidence |
|---------|---------|---------|-----------------|------------|
| Serilog | 4.3.1 | Structured logging | Industry standard for .NET structured logging. Key-value structured events queryable in analysis tools. Sink ecosystem covers every target (Console, File, Seq, Elasticsearch, Application Insights). | HIGH |
| Serilog.AspNetCore | 10.0.0 | ASP.NET Core integration | Version matches target framework. Routes ASP.NET Core log messages through Serilog. | HIGH |
| Serilog.Settings.Configuration | 10.0.0 | Config-based log settings | Configure Serilog from appsettings.json. | HIGH |

### Resilience

| Library | Version | Purpose | Why Recommended | Confidence |
|---------|---------|---------|-----------------|------------|
| Polly | 8.6.5 | Resilience / fault handling | Microsoft-recommended. Retry, circuit breaker, timeout, bulkhead isolation. Critical for YARP proxy calls to legacy app and any external service calls. Integrated with `Microsoft.Extensions.Http.Resilience`. | HIGH |

### Testing

| Library | Version | Purpose | Why Recommended | Confidence |
|---------|---------|---------|-----------------|------------|
| TUnit | 1.13.x (latest: ~1.13.56) | Unit test framework | Modern, source-generator based. Parallel execution by default. Native AOT support. Built on Microsoft.Testing.Platform (not legacy VSTest). Faster than xUnit/NUnit for large test suites. Active development with frequent releases. | HIGH |
| bUnit | 2.5.x (latest: 2.5.3) | Blazor component unit testing | The only dedicated Blazor component testing library. Renders components in-memory, asserts on markup and component state. Supports .NET 10 (net10.0 target added). Works with TUnit, xUnit, NUnit, and MSTest. | HIGH |
| Microsoft.Playwright | 1.58.0 | E2E browser testing | Microsoft-maintained. Cross-browser (Chromium, Firefox, WebKit). Reliable auto-wait. Network interception for test isolation. The E2E tests verifying Blazor/WebForms visual equivalence will use Playwright. | HIGH |
| TUnit.Playwright | 1.7.20 | TUnit + Playwright integration | Bridges TUnit test framework with Playwright. Provides test lifecycle hooks for browser/page management. | MEDIUM |
| Microsoft.AspNetCore.Mvc.Testing | 10.0.x | Integration testing | Ships with ASP.NET Core. `WebApplicationFactory<T>` for in-memory server testing. Test YARP routing, API endpoints, and middleware without deploying. | HIGH |

**Testing Strategy:**

```
Layer           | Tool            | What It Tests
----------------|-----------------|------------------------------------------
Unit (Domain)   | TUnit           | Business logic, validators, mappers
Unit (Blazor)   | TUnit + bUnit   | Component rendering, event handlers, state
Integration     | TUnit + WAF     | API endpoints, YARP routing, DI, middleware
E2E             | TUnit+Playwright| Full browser tests, visual equivalence
```

### Development Tools

| Tool | Purpose | Notes |
|------|---------|-------|
| Visual Studio 2026 | IDE | Required for .NET 10 targeting. VS 2022 cannot target .NET 10. |
| .NET 10 SDK | Build tooling | Install from https://dotnet.microsoft.com/download/dotnet/10.0 |
| dotnet ef CLI | EF Core scaffolding | `dotnet tool install --global dotnet-ef` |
| EF Core Power Tools | Visual scaffolding | VS extension. GUI for scaffold operations. |
| Playwright CLI | Browser install | `pwsh bin/Debug/net10.0/playwright.ps1 install` |

---

## Clean Architecture Layer Organization

### Layer Structure

```
src/
  AtlantaSignatur.Domain/              # Innermost layer - zero dependencies
  AtlantaSignatur.Application/          # Use cases, CQRS handlers, interfaces
  AtlantaSignatur.Infrastructure/       # EF Core, external services, YARP config
  AtlantaSignatur.Web/                  # Blazor components, pages, Program.cs

tests/
  AtlantaSignatur.Domain.Tests/         # TUnit - pure domain logic
  AtlantaSignatur.Application.Tests/    # TUnit - handler tests with mocked repos
  AtlantaSignatur.Infrastructure.Tests/ # TUnit - integration tests with real DB
  AtlantaSignatur.Web.Tests/            # TUnit + bUnit - component tests
  AtlantaSignatur.E2E.Tests/            # TUnit + Playwright - browser tests
```

### Layer Dependencies (Inward Only)

```
Web --> Application --> Domain
 |          |
 +---> Infrastructure --> Domain
```

- **Domain**: Entity classes (scaffolded + partial extensions), value objects, domain events, repository interfaces (`IActivityRepository`, etc.). Zero NuGet dependencies.
- **Application**: MediatR handlers (queries/commands), FluentValidation validators, Mapperly mappers (DTO definitions), application service interfaces. References: Domain. NuGet: MediatR, FluentValidation, Mapperly.
- **Infrastructure**: EF Core DbContext (scaffolded), repository implementations, System.Web Adapters configuration, external service clients. References: Domain, Application. NuGet: EF Core, System.Web Adapters, Polly, Serilog sinks.
- **Web**: Blazor components/pages, MudBlazor integration, YARP configuration, Program.cs (DI composition root), static assets. References: Application, Infrastructure. NuGet: MudBlazor, YARP, Serilog.AspNetCore.

### Repository Pattern with EF Core

Use thin repositories that wrap EF Core's DbContext. The repository interface lives in Domain; the implementation lives in Infrastructure:

```csharp
// Domain/Interfaces/IActivityRepository.cs
public interface IActivityRepository
{
    Task<Activity?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<PagedResult<Activity>> GetPagedAsync(ActivityFilter filter, CancellationToken ct = default);
}

// Infrastructure/Repositories/ActivityRepository.cs
public sealed class ActivityRepository(AppDbContext db) : IActivityRepository
{
    public async Task<Activity?> GetByIdAsync(int id, CancellationToken ct)
        => await db.Activities.FindAsync([id], ct);

    public async Task<PagedResult<Activity>> GetPagedAsync(ActivityFilter filter, CancellationToken ct)
    {
        var query = db.Activities.AsNoTracking();
        // Apply filters...
        return await query.ToPagedResultAsync(filter.Page, filter.PageSize, ct);
    }
}
```

**Why repositories with EF Core (not just raw DbContext):**
- Testability: Mock `IActivityRepository` in application-layer tests without EF Core dependencies.
- Immutable schema constraint: Repositories encapsulate the mapping between the legacy schema and clean domain concepts.
- Query encapsulation: Complex queries with legacy schema joins stay in Infrastructure, not in Blazor components.
- Future flexibility: If the database layer ever changes, only Infrastructure changes.

---

## Installation

```bash
# Create solution structure
dotnet new sln -n AtlantaSignatur
dotnet new classlib -n AtlantaSignatur.Domain -f net10.0
dotnet new classlib -n AtlantaSignatur.Application -f net10.0
dotnet new classlib -n AtlantaSignatur.Infrastructure -f net10.0
dotnet new blazor -n AtlantaSignatur.Web -f net10.0 --interactivity Server

# Add projects to solution
dotnet sln add src/AtlantaSignatur.Domain
dotnet sln add src/AtlantaSignatur.Application
dotnet sln add src/AtlantaSignatur.Infrastructure
dotnet sln add src/AtlantaSignatur.Web

# Domain layer - no external packages

# Application layer
dotnet add src/AtlantaSignatur.Application package MediatR --version 14.0.0
dotnet add src/AtlantaSignatur.Application package FluentValidation --version 12.1.1
dotnet add src/AtlantaSignatur.Application package FluentValidation.DependencyInjectionExtensions --version 12.1.1
dotnet add src/AtlantaSignatur.Application package Riok.Mapperly --version 4.3.1

# Infrastructure layer
dotnet add src/AtlantaSignatur.Infrastructure package Microsoft.EntityFrameworkCore.SqlServer --version 10.0.0
dotnet add src/AtlantaSignatur.Infrastructure package Microsoft.EntityFrameworkCore.Design --version 10.0.0
dotnet add src/AtlantaSignatur.Infrastructure package Microsoft.AspNetCore.SystemWebAdapters --version 2.3.0
dotnet add src/AtlantaSignatur.Infrastructure package Polly --version 8.6.5
dotnet add src/AtlantaSignatur.Infrastructure package Serilog --version 4.3.1

# Web layer
dotnet add src/AtlantaSignatur.Web package MudBlazor --version 8.15.0
dotnet add src/AtlantaSignatur.Web package Yarp.ReverseProxy --version 2.3.0
dotnet add src/AtlantaSignatur.Web package Serilog.AspNetCore --version 10.0.0
dotnet add src/AtlantaSignatur.Web package Serilog.Settings.Configuration --version 10.0.0

# Test projects
dotnet new classlib -n AtlantaSignatur.Domain.Tests -f net10.0
dotnet new classlib -n AtlantaSignatur.Application.Tests -f net10.0
dotnet new classlib -n AtlantaSignatur.Web.Tests -f net10.0
dotnet new classlib -n AtlantaSignatur.E2E.Tests -f net10.0

# Testing packages (all test projects)
dotnet add tests/AtlantaSignatur.Domain.Tests package TUnit
dotnet add tests/AtlantaSignatur.Application.Tests package TUnit
dotnet add tests/AtlantaSignatur.Web.Tests package TUnit
dotnet add tests/AtlantaSignatur.Web.Tests package bunit --version 2.5.3
dotnet add tests/AtlantaSignatur.E2E.Tests package TUnit
dotnet add tests/AtlantaSignatur.E2E.Tests package TUnit.Playwright
dotnet add tests/AtlantaSignatur.E2E.Tests package Microsoft.Playwright --version 1.58.0

# Integration testing
dotnet add tests/AtlantaSignatur.Application.Tests package Microsoft.AspNetCore.Mvc.Testing

# Install EF Core global tool
dotnet tool install --global dotnet-ef

# Scaffold existing database (run from Infrastructure project)
dotnet ef dbcontext scaffold "Server=...;Database=AtlantaSignatur;..." Microsoft.EntityFrameworkCore.SqlServer --output-dir Models --context-dir Data --context AppDbContext --no-onconfiguring
```

---

## Alternatives Considered

| Category | Recommended | Alternative | Why Not the Alternative |
|----------|-------------|-------------|------------------------|
| UI Framework | Blazor Interactive Server | Blazor WASM | WASM requires building a full API layer. Adds latency for initial download. Server-side rendering matches existing WebForms model. No benefit for an internal recruitment tool. |
| UI Framework | Blazor Interactive Server | Blazor Auto | Auto mode complexity not justified. The app is primarily used from office networks with stable connections. Server mode simplicity wins. |
| Component Library | MudBlazor | Telerik Blazor | Telerik requires commercial license. MudBlazor is MIT-licensed, has comparable components, and a larger community. |
| Component Library | MudBlazor | Syncfusion Blazor | Syncfusion has a free community license but is more complex. MudBlazor's API is simpler and better documented for Material Design patterns. |
| ORM | EF Core 10 (Database-First) | Dapper | EF Core scaffolding generates the full model from existing schema automatically. Dapper would require manual SQL for every query. EF Core's LINQ composition is more maintainable for the 100+ entity types in a typical ATS schema. |
| Object Mapping | Mapperly | AutoMapper | AutoMapper uses runtime reflection. Mapperly is source-generated (compile-time), faster, catches errors at build time. AutoMapper is the legacy choice; Mapperly is the 2025-2026 standard for greenfield. |
| Mediator | MediatR | Wolverine | MediatR has broader ecosystem, more documentation, established patterns. Wolverine is more powerful (built-in messaging, sagas) but heavier. Use Wolverine only if MediatR licensing is prohibitive. |
| Mediator | MediatR | No mediator (direct service calls) | For a 10+ year maintainability target, the indirection MediatR provides is worth it. Pipeline behaviors for validation/logging/caching reduce cross-cutting concern boilerplate. |
| Test Framework | TUnit | xUnit | TUnit is source-generated (faster compilation, better IDE integration), parallel by default, built on Microsoft.Testing.Platform (modern). xUnit is legacy VSTest-based. For a greenfield test suite, TUnit is the forward-looking choice. |
| Test Framework | TUnit | NUnit | Same reasoning as xUnit. TUnit is newer and faster. NUnit would work but offers no advantage over TUnit for a new project. |
| E2E Testing | Playwright | Selenium | Playwright is faster, more reliable (auto-wait), cross-browser, better .NET integration. Selenium is legacy. |
| E2E Testing | Playwright | Cypress | Cypress is JavaScript-only. Playwright has first-class .NET support. Keep the entire test stack in C#. |
| Logging | Serilog | NLog | Both are mature. Serilog's structured logging API is more natural for modern .NET. Larger sink ecosystem. Better integration with ASP.NET Core via Serilog.AspNetCore. |
| Logging | Serilog | Microsoft.Extensions.Logging alone | M.E.L is the abstraction layer (Serilog plugs into it). Serilog adds structured events, rich sinks, and better filtering. Use both together. |
| Resilience | Polly | Custom retry logic | Polly is Microsoft-recommended, battle-tested, composable. Rolling your own is error-prone. |

---

## What NOT to Use

| Avoid | Why | Use Instead |
|-------|-----|-------------|
| AutoMapper | Runtime reflection overhead. Configuration errors only found at runtime. Mapperly is strictly better for new projects. | Riok.Mapperly 4.3.1 |
| Blazor WebAssembly (for this project) | Requires full API layer. Adds WASM download latency. Unnecessary for a server-rendered recruitment app. | Blazor Interactive Server |
| xUnit / NUnit (for new test projects) | Legacy VSTest platform. TUnit is modern, source-generated, faster. No reason to start new test projects on old frameworks. | TUnit 1.13.x |
| Selenium | Slow, flaky, requires WebDriver management. Playwright is faster and more reliable. | Playwright 1.58.0 |
| BinaryFormatter | Removed in .NET 10 for security reasons. System.Web Adapters session serialization must use JSON serializer. | AddJsonSessionSerializer() |
| EF Core Migrations | The database schema is immutable. Never generate or apply migrations. Use scaffold-only (database-first). | `dotnet ef dbcontext scaffold` |
| SignalR manual hub (for UI) | Blazor Server already uses SignalR under the hood. Adding custom hubs for UI updates adds unnecessary complexity. | Blazor Interactive Server built-in SignalR |
| In-Process session state | Both apps need to share session during migration. In-process sessions are not shareable. | Remote App Session via System.Web Adapters |
| MudBlazor 7.x or earlier | Outdated. Does not support .NET 9/10. | MudBlazor 8.15.0+ |
| Telerik/Syncfusion (without license) | Commercial components require licensing. MudBlazor covers all needed components without licensing cost. | MudBlazor (MIT) |

---

## Version Compatibility Matrix

| Package | Version | Compatible With | Notes |
|---------|---------|-----------------|-------|
| .NET 10 SDK | 10.0.3 | VS 2026 only | VS 2022 cannot target .NET 10 |
| EF Core 10 | 10.0.x | .NET 10 only | EF 10 requires .NET 10 runtime |
| YARP | 2.3.0 | .NET 8+ | Works on .NET 10 without changes |
| System.Web Adapters | 2.3.0 | .NET 8+ / .NET Framework 4.7.2+ | Bridges both sides of the migration |
| MudBlazor | 8.15.0 | .NET 8, .NET 9 | Works on .NET 10 with 3 minor component issues |
| MudBlazor | 9.0.0-preview.1 | .NET 8, .NET 9, .NET 10 | Preview; wait for stable release |
| MediatR | 14.0.0 | .NET 8+ | Dual license (RPL-1.5 + commercial). Free Community Edition for <$5M revenue |
| FluentValidation | 12.1.1 | .NET 8+ | Fully compatible with .NET 10 |
| Mapperly | 4.3.1 | .NET Standard 2.0+ | Source generator; works with any .NET version |
| Serilog.AspNetCore | 10.0.0 | .NET 10 | Version tracks target framework |
| Polly | 8.6.5 | .NET 6+ | Fully compatible with .NET 10 |
| TUnit | 1.13.x | .NET 8+ | Built on Microsoft.Testing.Platform |
| bUnit | 2.5.3 | .NET 8, .NET 10 | Dropped pre-.NET 8 support. Added net10.0 target |
| Playwright | 1.58.0 | .NET Standard 2.0+ | Framework-agnostic |
| TUnit.Playwright | 1.7.20 | TUnit 1.x + Playwright 1.52+ | Integration bridge |

---

## Stack Patterns by Migration Phase

**Phase: Foundation (YARP + System.Web Adapters setup)**
- All requests flow through the new ASP.NET Core app
- YARP forwards everything to legacy app initially
- System.Web Adapters configured for session/auth sharing
- Zero new Blazor UI yet

**Phase: First Component Migration (Activity List View)**
- Activity list route handled by Blazor + MudBlazor
- All other routes still forwarded to legacy WebForms app
- YARP routes updated: `/activities` -> Blazor, everything else -> legacy
- Playwright E2E tests verify visual equivalence with original

**Phase: Progressive Migration**
- Route by route, WebForms pages replaced with Blazor components
- YARP route table shrinks as more pages are migrated
- Session sharing ensures users move seamlessly between old and new pages

**Phase: Legacy Decommission**
- All routes served by Blazor
- YARP proxy removed
- System.Web Adapters removed
- Clean ASP.NET Core 10 application remains

---

## YARP Configuration Reference

```json
{
  "ReverseProxy": {
    "Routes": {
      "legacyFallback": {
        "ClusterId": "legacyCluster",
        "Match": {
          "Path": "{**catch-all}"
        },
        "Order": 10000
      }
    },
    "Clusters": {
      "legacyCluster": {
        "Destinations": {
          "legacyApp": {
            "Address": "https://localhost:44300"
          }
        }
      }
    }
  }
}
```

The `Order: 10000` ensures migrated Blazor routes (registered with lower order via `MapRazorComponents`) take precedence. Only unmatched requests fall through to the legacy app.

---

## Sources

### Official Microsoft Documentation (HIGH confidence)
- [What's new in ASP.NET Core 10](https://learn.microsoft.com/en-us/aspnet/core/release-notes/aspnetcore-10.0?view=aspnetcore-10.0)
- [What's new in .NET 10](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-10/overview)
- [Getting started with YARP](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/servers/yarp/getting-started?view=aspnetcore-10.0)
- [YARP Overview](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/servers/yarp/yarp-overview?view=aspnetcore-10.0)
- [System.Web Adapters](https://learn.microsoft.com/en-us/aspnet/core/migration/fx-to-core/inc/systemweb-adapters?view=aspnetcore-10.0)
- [Incremental ASP.NET to ASP.NET Core Migration](https://learn.microsoft.com/en-us/aspnet/core/migration/fx-to-core/start?view=aspnetcore-10.0)
- [Migrate ASP.NET Framework Session](https://learn.microsoft.com/en-us/aspnet/core/migration/fx-to-core/areas/session?view=aspnetcore-10.0)
- [Migrate ASP.NET Framework Authentication](https://learn.microsoft.com/en-us/aspnet/core/migration/fx-to-core/areas/authentication?view=aspnetcore-10.0)
- [EF Core Scaffolding (Reverse Engineering)](https://learn.microsoft.com/en-us/ef/core/managing-schemas/scaffolding/)
- [What's new in EF Core 10](https://learn.microsoft.com/en-us/ef/core/what-is-new/ef-core-10.0/whatsnew)
- [Blazor Render Modes](https://learn.microsoft.com/en-us/aspnet/core/blazor/components/render-modes?view=aspnetcore-10.0)
- [.NET 10 Release Notes](https://github.com/dotnet/core/blob/main/release-notes/10.0/README.md)
- [Announcing .NET 10](https://devblogs.microsoft.com/dotnet/announcing-dotnet-10/)

### NuGet Package Pages (HIGH confidence - version verification)
- [Yarp.ReverseProxy 2.3.0](https://www.nuget.org/packages/Yarp.ReverseProxy)
- [Microsoft.AspNetCore.SystemWebAdapters 2.3.0](https://www.nuget.org/packages/Microsoft.AspNetCore.SystemWebAdapters)
- [MudBlazor 8.15.0](https://www.nuget.org/packages/MudBlazor)
- [MudBlazor 9.0.0-preview.1](https://www.nuget.org/packages/MudBlazor/9.0.0-preview.1)
- [MediatR 14.0.0](https://www.nuget.org/packages/mediatr/)
- [FluentValidation 12.1.1](https://www.nuget.org/packages/fluentvalidation/)
- [Riok.Mapperly 4.3.1](https://www.nuget.org/packages/Riok.Mapperly)
- [Serilog 4.3.1](https://www.nuget.org/packages/Serilog/4.3.1)
- [Serilog.AspNetCore 10.0.0](https://www.nuget.org/packages/serilog.aspnetcore)
- [Polly 8.6.5](https://www.nuget.org/packages/polly/)
- [TUnit 1.13.11](https://www.nuget.org/packages/TUnit/)
- [bUnit 2.5.3](https://www.nuget.org/packages/bunit/)
- [Microsoft.Playwright 1.58.0](https://www.nuget.org/packages/microsoft.playwright)
- [TUnit.Playwright 1.7.20](https://www.nuget.org/packages/TUnit.Playwright/1.7.20)

### GitHub Repositories (HIGH confidence)
- [dotnet/yarp](https://github.com/dotnet/yarp)
- [dotnet/systemweb-adapters](https://github.com/dotnet/systemweb-adapters)
- [MudBlazor/MudBlazor](https://github.com/MudBlazor/MudBlazor)
- [thomhurst/TUnit](https://github.com/thomhurst/TUnit)
- [riok/mapperly](https://github.com/riok/mapperly)
- [bUnit-dev/bUnit](https://github.com/bUnit-dev/bUnit)

### Community / Analysis Sources (MEDIUM confidence)
- [Trailhead: Upgrading WebForms to Blazor with YARP](https://trailheadtechnology.com/upgrading-an-asp-net-web-forms-app-to-blazor-incrementally-with-yarp/)
- [MudBlazor .NET 10 Discussion](https://github.com/MudBlazor/MudBlazor/discussions/12122)
- [MudBlazor .NET 10 Target Issue](https://github.com/MudBlazor/MudBlazor/issues/12049)
- [ABP.IO: Why We Moved to Mapperly](https://abp.io/community/articles/best-free-alternatives-to-automapper-in-.net-why-we-moved-to-mapperly-l9f5ii8s)
- [Clean Architecture with Blazor Server](https://github.com/neozhu/CleanArchitectureWithBlazorServer)
- [The Ultimate Clean Architecture Guide for .NET 9 (2026 Edition)](https://medium.com/@kerimkkara/the-ultimate-clean-architecture-guide-for-net-9-2026-edition-0b4a37eeef64)
- [10 Architecture Mistakes in Blazor Projects](https://medium.com/dotnet-new/10-architecture-mistakes-developers-make-in-blazor-projects-and-how-to-fix-them-e99466006e0d)
- [Blazor Prerendering Fixed in .NET 10](https://dotnetwebacademy.substack.com/p/net-10-finally-fixes-prerendering)
- [MediatR Licensing (RPL-1.5)](https://dariusz-wozniak.github.io/fossed/library/mediatr)

---

*Stack research for: AtlantaSignatur ASP.NET WebForms to .NET 10 Blazor incremental migration*
*Researched: 2026-02-13*
