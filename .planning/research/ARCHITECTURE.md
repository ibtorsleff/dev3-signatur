# Architecture Research

**Domain:** ATS (Applicant Tracking System) platform modernization -- WebForms to Blazor via incremental migration
**Researched:** 2026-02-13
**Confidence:** HIGH

## Standard Architecture

### System Overview

```
                           INTERNET
                              |
                    +---------+---------+
                    |   ASP.NET Core    |
                    |   Host (Kestrel)  |
                    |                   |
                    |  +-----------+    |         +------------------+
                    |  |   YARP    |----+-------->| Legacy WebForms  |
                    |  | Fallback  |    |         | (.NET Framework) |
                    |  +-----------+    |         +--------+---------+
                    |       |           |                  |
                    |  +-----------+    |                  |
                    |  |  Blazor   |    |    System.Web Adapters
                    |  | SSR/WASM  |    |    (session + auth sharing)
                    |  +-----+-----+    |                  |
                    +--------|----------+                  |
                             |                             |
        +--------------------+--------------------+        |
        |                                         |        |
   +----+------+    +------------+    +-----------+--------+--+
   |  Web/UI   |    | Application|    |    Infrastructure     |
   |  Project  +--->+   (Use     +--->+    Project            |
   |  (Blazor) |    |   Cases)   |    |  (EF Core, External) |
   +----+------+    +-----+------+    +----------+-----------+
        |                 |                      |
        |           +-----+------+               |
        +---------->+   Domain   +<--------------+
                    |   (Core)   |
                    +-----+------+
                          |
                    +-----+------+
                    | Shared     |
                    | Kernel     |
                    +------------+
```

**Dependency Rule (inward only):**
- Web/UI --> Application --> Domain <-- Infrastructure
- Domain depends on nothing. Infrastructure implements Domain interfaces.
- Application orchestrates Domain objects via interfaces. Never touches Infrastructure directly.
- Web/UI references Application (for use cases) and Domain (for entities/DTOs). References Infrastructure only at composition root (Program.cs) for DI registration.

### Component Responsibilities

| Component | Responsibility | Typical Implementation |
|-----------|----------------|------------------------|
| **Domain (Core)** | Business entities, value objects, domain events, interfaces (IRepository, IUnitOfWork, ITenantService), specifications, enums, exceptions | Pure C# class library. Zero external dependencies. No NuGet packages except analyzers. |
| **Application (Use Cases)** | Application services, command/query handlers, DTO definitions, validation, mapping extensions, cross-cutting behaviors | C# class library. References Domain only. Contains `IApplicationDbContext` interface if needed. |
| **Infrastructure** | EF Core DbContext, repository implementations, external service integrations, file storage, email, job board APIs | C# class library. References Domain. Implements all infrastructure interfaces. |
| **Web (UI Host)** | Blazor components (SSR + Interactive), pages, layouts, YARP configuration, DI composition root, middleware pipeline, MudBlazor theming | ASP.NET Core project. References all layers. Wires DI. Hosts YARP. |
| **SharedKernel** | Base entity classes, common value objects, guard clauses, result types, common interfaces | Optional C# class library. Referenced by Domain. Shared across bounded contexts if needed. |

## Recommended Project Structure

```
Signatur.sln
|
+-- src/
|   +-- Signatur.Domain/                    # Domain layer (innermost)
|   |   +-- Entities/
|   |   |   +-- Recruitment/
|   |   |   |   +-- ERActivity.cs
|   |   |   |   +-- ERCandidate.cs
|   |   |   |   +-- ERApplicationTemplate.cs
|   |   |   +-- JobPosting/
|   |   |   |   +-- WebAd.cs
|   |   |   |   +-- Insertion.cs
|   |   |   +-- UserManagement/
|   |   |   |   +-- User.cs
|   |   |   +-- Tenancy/
|   |   |       +-- Client.cs
|   |   |       +-- ClientSection.cs
|   |   +-- Interfaces/
|   |   |   +-- Repositories/
|   |   |   |   +-- IRepository.cs          # Generic repository interface
|   |   |   |   +-- IReadRepository.cs       # Read-only variant
|   |   |   |   +-- IERActivityRepository.cs # Domain-specific when needed
|   |   |   +-- IUnitOfWork.cs
|   |   |   +-- ITenantProvider.cs
|   |   |   +-- ICurrentUserService.cs
|   |   +-- Enums/
|   |   +-- Exceptions/
|   |   +-- Events/                          # Domain events
|   |   +-- Specifications/                  # Specification pattern (optional)
|   |   +-- ValueObjects/
|   |   +-- Signatur.Domain.csproj           # NO external dependencies
|   |
|   +-- Signatur.Application/               # Application layer
|   |   +-- Common/
|   |   |   +-- Interfaces/
|   |   |   |   +-- IApplicationDbContext.cs  # Thin DbContext interface (optional)
|   |   |   +-- Mappings/
|   |   |   |   +-- ActivityMappingExtensions.cs
|   |   |   |   +-- CandidateMappingExtensions.cs
|   |   |   +-- Models/                      # DTOs
|   |   |   |   +-- ActivityDto.cs
|   |   |   |   +-- CandidateDto.cs
|   |   |   |   +-- PaginatedList.cs
|   |   |   +-- Behaviors/                   # Cross-cutting (logging, validation)
|   |   |   +-- Exceptions/
|   |   +-- Features/                        # Organized by feature/use case
|   |   |   +-- Recruitment/
|   |   |   |   +-- Queries/
|   |   |   |   |   +-- GetActivities/
|   |   |   |   |   |   +-- GetActivitiesQuery.cs
|   |   |   |   |   |   +-- GetActivitiesHandler.cs
|   |   |   |   +-- Commands/
|   |   |   |       +-- CreateCandidate/
|   |   |   |           +-- CreateCandidateCommand.cs
|   |   |   |           +-- CreateCandidateHandler.cs
|   |   |   +-- WebAds/
|   |   |   +-- Onboarding/
|   |   +-- Signatur.Application.csproj      # References: Signatur.Domain
|   |
|   +-- Signatur.Infrastructure/             # Infrastructure layer
|   |   +-- Data/
|   |   |   +-- SignaturDbContext.cs          # EF Core DbContext (existing schema)
|   |   |   +-- Configurations/              # IEntityTypeConfiguration<T> classes
|   |   |   |   +-- ERActivityConfiguration.cs
|   |   |   |   +-- ERCandidateConfiguration.cs
|   |   |   +-- Interceptors/
|   |   |   |   +-- TenantInterceptor.cs     # Enforces tenant filtering
|   |   |   |   +-- AuditableInterceptor.cs
|   |   +-- Repositories/
|   |   |   +-- Repository.cs               # Generic implementation
|   |   |   +-- UnitOfWork.cs
|   |   |   +-- ERActivityRepository.cs     # Domain-specific when needed
|   |   +-- Services/
|   |   |   +-- TenantProvider.cs
|   |   |   +-- CurrentUserService.cs
|   |   +-- Signatur.Infrastructure.csproj   # References: Signatur.Domain, EF Core packages
|   |
|   +-- Signatur.Web/                        # Web/UI host layer
|   |   +-- Components/
|   |   |   +-- Pages/                       # Blazor page components (.razor)
|   |   |   |   +-- Recruitment/
|   |   |   |   +-- WebAds/
|   |   |   +-- Shared/                      # Layout, NavMenu, shared components
|   |   |   +-- App.razor
|   |   |   +-- Routes.razor
|   |   +-- wwwroot/                         # Static assets
|   |   +-- Configuration/
|   |   |   +-- DependencyInjection.cs       # DI registration helpers
|   |   +-- Program.cs                       # Composition root: DI, YARP, middleware
|   |   +-- appsettings.json                 # YARP config, connection strings
|   |   +-- Signatur.Web.csproj             # References: all projects
|   |
|   +-- Signatur.SharedKernel/              # Optional shared kernel
|       +-- BaseEntity.cs
|       +-- IAuditableEntity.cs
|       +-- ITenantEntity.cs                 # Interface for tenant-filtered entities
|       +-- Result.cs                        # Result<T> pattern
|       +-- GuardClauses/
|
+-- tests/
|   +-- Signatur.Domain.Tests/
|   +-- Signatur.Application.Tests/
|   +-- Signatur.Infrastructure.Tests/
|   +-- Signatur.Web.Tests/                  # Integration + Playwright E2E
|
+-- Signatur.sln
```

### Structure Rationale

- **Domain organized by bounded context (Recruitment, JobPosting, etc.):** Mirrors the database functional domains documented in SigDB.MD. Keeps related entities together. Avoids a single flat Entities folder with 100+ classes.
- **Application organized by Feature:** Each feature folder contains its queries, commands, DTOs, and validators together. Reduces cognitive load vs. organizing by type (all queries in one folder, all commands in another).
- **Infrastructure keeps EF Core isolated:** Only the Infrastructure project knows about EF Core. The Domain and Application layers have zero awareness of the ORM. Entity configurations live alongside the DbContext, not polluting entity classes.
- **Web project is thin:** Contains only Blazor components, Program.cs (composition root), and YARP configuration. All business logic lives in Application; all data access in Infrastructure.
- **SharedKernel is optional but recommended:** Provides base classes (`BaseEntity`, `ITenantEntity`) to avoid duplication. Should contain truly shared abstractions only -- not a dumping ground.

## Architectural Patterns

### Pattern 1: Repository + Unit of Work (Over EF Core)

**What:** Repository abstracts data access behind domain interfaces. Unit of Work coordinates transactions across multiple repositories. EF Core's `DbContext` already implements both patterns internally, so our abstraction is thin and intentional.

**When to use:** Use because the project constraint requires a 10+ year maintainability window. Abstracting EF Core provides the ability to swap or upgrade ORMs, enables unit testing with in-memory fakes, and keeps Domain/Application layers ignorant of EF Core specifics. This is not premature abstraction -- it is insurance for a decade-long codebase.

**Trade-offs:** Adds one layer of indirection. Requires discipline not to leak `IQueryable` outside the repository (return materialized results). Small upfront cost, large long-term payoff.

**Confidence:** HIGH -- Microsoft's own architecture guidance recommends this pattern for non-trivial applications. The ardalis Clean Architecture template uses it. The eShopOnWeb reference application uses it.

**Example:**
```csharp
// Domain/Interfaces/Repositories/IRepository.cs
public interface IRepository<T> where T : BaseEntity
{
    Task<T?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<T>> ListAllAsync(CancellationToken ct = default);
    Task<T> AddAsync(T entity, CancellationToken ct = default);
    void Update(T entity);
    void Delete(T entity);
}

// Domain/Interfaces/IUnitOfWork.cs
public interface IUnitOfWork : IDisposable
{
    IRepository<ERActivity> Activities { get; }
    IRepository<ERCandidate> Candidates { get; }
    IRepository<WebAd> WebAds { get; }
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}

// Infrastructure/Repositories/Repository.cs
public class Repository<T> : IRepository<T> where T : BaseEntity
{
    private readonly SignaturDbContext _context;
    private readonly DbSet<T> _dbSet;

    public Repository(SignaturDbContext context)
    {
        _context = context;
        _dbSet = context.Set<T>();
    }

    public async Task<T?> GetByIdAsync(int id, CancellationToken ct = default)
        => await _dbSet.FindAsync(new object[] { id }, ct);

    public async Task<IReadOnlyList<T>> ListAllAsync(CancellationToken ct = default)
        => await _dbSet.ToListAsync(ct);

    public async Task<T> AddAsync(T entity, CancellationToken ct = default)
    {
        await _dbSet.AddAsync(entity, ct);
        return entity;
    }

    public void Update(T entity) => _dbSet.Update(entity);
    public void Delete(T entity) => _dbSet.Remove(entity);
}

// Infrastructure/Repositories/UnitOfWork.cs
public class UnitOfWork : IUnitOfWork
{
    private readonly SignaturDbContext _context;

    public UnitOfWork(SignaturDbContext context)
    {
        _context = context;
        Activities = new Repository<ERActivity>(context);
        Candidates = new Repository<ERCandidate>(context);
        WebAds = new Repository<WebAd>(context);
    }

    public IRepository<ERActivity> Activities { get; }
    public IRepository<ERCandidate> Candidates { get; }
    public IRepository<WebAd> WebAds { get; }

    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
        => await _context.SaveChangesAsync(ct);

    public void Dispose() => _context.Dispose();
}
```

**Important: DbContext lifetime in Blazor.** In Blazor Server/Interactive Server apps, `DbContext` must not be scoped or singleton. Use `IDbContextFactory<SignaturDbContext>` registered as Scoped (not Singleton, because multi-tenancy requires per-user configuration). Each operation creates and disposes its own `DbContext` via the factory. This is the Microsoft-recommended pattern for Blazor + EF Core.

```csharp
// In Program.cs (composition root)
builder.Services.AddDbContextFactory<SignaturDbContext>((sp, options) =>
{
    var tenant = sp.GetRequiredService<ITenantProvider>();
    options.UseSqlServer(tenant.GetConnectionString());
}, ServiceLifetime.Scoped);  // Scoped for multi-tenancy
```

### Pattern 2: Manual DTO Mapping via Extension Methods

**What:** Static extension methods that map between domain entities and DTOs. No AutoMapper, no Mapster, no reflection-based mapping.

**When to use:** Always, for this project. AutoMapper became commercial in April 2025. Manual mapping provides compile-time safety, explicit control, easy debugging, and zero runtime overhead. For a 10+ year project, eliminating a third-party mapping dependency is wise.

**Trade-offs:** More code to write per mapping. Mitigated by keeping DTOs focused (fewer properties per DTO) and by using consistent patterns. The compile-time safety and debuggability more than compensate.

**Confidence:** HIGH -- Manual mapping is well-established in the .NET community. Multiple Clean Architecture references (Jason Taylor, ardalis) support this approach. AutoMapper's commercialization has accelerated the shift.

**Example:**
```csharp
// Application/Common/Mappings/ActivityMappingExtensions.cs
public static class ActivityMappingExtensions
{
    public static ActivityDto ToDto(this ERActivity entity)
    {
        return new ActivityDto
        {
            Id = entity.Id,
            Title = entity.Title,
            Status = entity.Status,
            ClientId = entity.ClientId,
            CreatedDate = entity.CreatedDate,
            CandidateCount = entity.Candidates?.Count ?? 0
        };
    }

    public static IReadOnlyList<ActivityDto> ToDtoList(this IEnumerable<ERActivity> entities)
        => entities.Select(e => e.ToDto()).ToList();

    // For EF Core projections (executes in SQL, not in memory):
    public static IQueryable<ActivityDto> ProjectToDto(this IQueryable<ERActivity> query)
    {
        return query.Select(e => new ActivityDto
        {
            Id = e.Id,
            Title = e.Title,
            Status = e.Status,
            ClientId = e.ClientId,
            CreatedDate = e.CreatedDate,
            CandidateCount = e.Candidates.Count
        });
    }
}

// Application/Common/Models/ActivityDto.cs
public sealed record ActivityDto
{
    public int Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public int ClientId { get; init; }
    public DateTime CreatedDate { get; init; }
    public int CandidateCount { get; init; }
}
```

**Key conventions:**
- `ToDto()` for in-memory mapping of loaded entities
- `ProjectToDto()` for IQueryable projections (translates to SQL SELECT)
- DTOs are sealed records (immutable, value equality)
- Mapping extensions live in Application layer (they know about both Domain entities and Application DTOs)

### Pattern 3: Two-Level Multi-Tenancy (Site -> Client)

**What:** Hierarchical multi-tenancy enforced at the EF Core level using global query filters. The existing database uses `Client` (top-level tenant, ~1534 records) and `ClientSection` (sub-tenant/department, ~30,405 records). Every data query must be scoped to the current tenant.

**When to use:** Every data access operation. This is a security boundary, not an optional feature.

**Trade-offs:** Global query filters add a WHERE clause to every query. Minimal performance impact (SQL Server optimizes well with indexed tenant columns). The risk of forgetting a filter without this pattern is far greater than the slight overhead.

**Confidence:** HIGH -- EF Core's global query filters are the Microsoft-recommended approach for multi-tenancy. EF Core 10 adds named query filters, making it even better for combining tenant filters with soft-delete filters.

**Example:**
```csharp
// Domain/Interfaces/ITenantProvider.cs
public interface ITenantProvider
{
    int SiteId { get; }          // Top-level tenant (Client)
    int? ClientSectionId { get; } // Sub-tenant (ClientSection), nullable for site-wide ops
    string GetConnectionString(); // If using database-per-tenant variant
}

// SharedKernel/ITenantEntity.cs
public interface ITenantEntity
{
    int ClientId { get; }  // Maps to Client.Id (SiteId)
}

public interface ISectionScopedEntity : ITenantEntity
{
    int? ClientSectionId { get; }  // Maps to ClientSection.Id
}

// Infrastructure/Data/SignaturDbContext.cs
public class SignaturDbContext : DbContext
{
    private readonly int _siteId;
    private readonly int? _clientSectionId;

    public SignaturDbContext(
        DbContextOptions<SignaturDbContext> options,
        ITenantProvider tenantProvider)
        : base(options)
    {
        _siteId = tenantProvider.SiteId;
        _clientSectionId = tenantProvider.ClientSectionId;
    }

    public DbSet<ERActivity> Activities => Set<ERActivity>();
    public DbSet<ERCandidate> Candidates => Set<ERCandidate>();
    public DbSet<WebAd> WebAds => Set<WebAd>();
    // ... other DbSets

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply entity configurations from assembly
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(SignaturDbContext).Assembly);

        // EF Core 10: Named query filters for two-level tenancy
        modelBuilder.Entity<ERActivity>()
            .HasQueryFilter("TenantFilter", a => a.ClientId == _siteId);

        modelBuilder.Entity<ERCandidate>()
            .HasQueryFilter("TenantFilter", c => c.ClientId == _siteId);

        // For section-scoped entities, apply both filters
        modelBuilder.Entity<WebAd>()
            .HasQueryFilter("TenantFilter", w => w.ClientId == _siteId)
            .HasQueryFilter("SectionFilter",
                w => _clientSectionId == null || w.ClientSectionId == _clientSectionId);
    }
}
```

**Enforcement points (defense in depth):**

| Layer | Mechanism | Purpose |
|-------|-----------|---------|
| Infrastructure (DbContext) | EF Core global query filters | Automatic WHERE clause on every query. Primary defense. |
| Infrastructure (SaveChanges interceptor) | `SaveChangesInterceptor` | Validates that new/updated entities have correct tenant ID before persisting. Prevents accidental cross-tenant writes. |
| Application (Use Case handlers) | `ITenantProvider` injected | Use cases can check tenant context. Secondary validation. |
| Web (Middleware) | Tenant resolution middleware | Resolves tenant from auth claims/session on every request. Sets `ITenantProvider`. |
| Web (Blazor circuit) | Circuit-scoped `ITenantProvider` | Tenant remains consistent for duration of Blazor circuit. |

### Pattern 4: YARP Strangler Fig Integration

**What:** The new ASP.NET Core/Blazor application sits in front of the legacy WebForms app using YARP as a reverse proxy. Routes that have been migrated are handled by the new app; all other requests fall through to the legacy app. This is the Strangler Fig pattern.

**When to use:** For the entire duration of the migration (potentially months/years). The new app starts handling zero routes and gradually takes over all routes.

**Trade-offs:** Adds an HTTP hop for legacy requests. Requires both apps to be running. System.Web Adapters add latency for session/auth sharing. The benefit (zero-downtime incremental migration) dramatically outweighs the costs.

**Confidence:** HIGH -- This is Microsoft's official recommended approach for ASP.NET Framework to ASP.NET Core migration. Documented extensively in the migration guides for ASP.NET Core 10.

**Implementation:**
```csharp
// Program.cs (Signatur.Web)
var builder = WebApplication.CreateBuilder(args);

// --- Service Registration ---
// System.Web Adapters (session + auth sharing with legacy app)
builder.Services.AddSystemWebAdapters()
    .AddRemoteAppClient(options =>
    {
        options.RemoteAppUrl = new(builder.Configuration["LegacyApp:Url"]);
        options.ApiKey = builder.Configuration["LegacyApp:ApiKey"];
    })
    .AddRemoteAppAuthentication()   // Defer auth to legacy app
    .AddRemoteAppSession();         // Share session with legacy app

// YARP reverse proxy
builder.Services.AddReverseProxy();

// EF Core, Application services, Domain services (via DI extension methods)
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplicationServices();

// Blazor
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// MudBlazor
builder.Services.AddMudServices();

var app = builder.Build();

// --- Middleware Pipeline ---
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseSystemWebAdapters();  // Must be after auth, before endpoints
app.UseAntiforgery();

// Map Blazor endpoints (migrated pages)
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// YARP fallback: everything not handled by Blazor goes to legacy app
app.MapForwarder(
    "/{**catch-all}",
    builder.Configuration["LegacyApp:Url"]!)
    .WithOrder(int.MaxValue)    // Lowest priority -- only matches unhandled routes
    .ShortCircuit();            // Skip remaining middleware for proxied requests

app.Run();
```

**Configuration (appsettings.json):**
```json
{
  "LegacyApp": {
    "Url": "https://localhost:44300",
    "ApiKey": "12345678-1234-1234-1234-123456789012"
  },
  "ConnectionStrings": {
    "SignaturDb": "Server=...;Database=SignaturAnnoncePortal;..."
  }
}
```

**Route migration strategy:**
1. Initially, YARP forwards 100% of requests to legacy app
2. As Blazor pages are built, they claim routes (e.g., `/recruitment/activities`)
3. Claimed routes are handled by Blazor; unclaimed routes fall through to YARP
4. Migration is complete when YARP forwards zero requests

## Data Flow

### Request Flow (Blazor Page -> Database)

```
[User clicks button in Blazor component]
    |
    v
[Blazor Component] -- calls -->
    |
    v
[Application Service / Use Case Handler]
    |  - Validates input
    |  - Resolves tenant from ITenantProvider
    |  - Calls repository via IUnitOfWork
    |
    v
[IUnitOfWork.Activities.GetByIdAsync(id)]
    |
    v
[Repository<ERActivity>] -- uses -->
    |
    v
[SignaturDbContext] -- EF Core translates to SQL -->
    |  - Global query filter adds: WHERE ClientId = @siteId
    |  - Executes query
    |
    v
[SQL Server: SignaturAnnoncePortal database]
    |
    v
[Entity returned to Repository]
    |
    v
[Entity mapped to DTO via .ToDto() extension]
    |
    v
[DTO returned to Blazor Component]
    |
    v
[Component renders via MudBlazor]
```

### YARP Fallback Flow (Unmigrated Route)

```
[User navigates to /legacy-page.aspx]
    |
    v
[ASP.NET Core Middleware Pipeline]
    |  - UseAuthentication() -- checks auth
    |  - UseSystemWebAdapters() -- loads shared session
    |  - No Blazor route matches
    |
    v
[YARP MapForwarder catch-all (order: int.MaxValue)]
    |  - Matches because no other route claimed this path
    |  - .ShortCircuit() skips further middleware
    |
    v
[HTTP request proxied to legacy WebForms app]
    |  - Forwards headers (including auth cookies)
    |  - Legacy app processes the request
    |
    v
[Response returned through YARP to user]
```

### Key Data Flows

1. **Tenant resolution:** HTTP request arrives -> Middleware reads tenant claim from auth token/session -> `ITenantProvider` (scoped) is populated -> All subsequent DI-resolved services (DbContext, repositories) automatically use the correct tenant.

2. **Blazor component data loading:** Component `OnInitializedAsync` -> Inject `IDbContextFactory` or Application Service -> Factory creates tenant-scoped DbContext -> Query executes with global filter -> Results mapped to DTOs -> Component state updated -> UI re-renders.

3. **Cross-app session sharing:** User has session in legacy WebForms -> Navigates to migrated Blazor page -> System.Web Adapters `RemoteAppSession` makes HTTP call to legacy app's session endpoint -> Session data deserialized into ASP.NET Core session -> Blazor page reads session data.

## Scaling Considerations

| Scale | Architecture Adjustments |
|-------|--------------------------|
| Current (existing load) | Single ASP.NET Core host + YARP to legacy. No changes needed. Focus on correctness. |
| Growth (2-5x users) | Add output caching for read-heavy pages. Consider read replicas for reporting queries. DbContext pooling if instantiation becomes measurable. |
| High scale (10x+) | Split into Blazor WASM for client-heavy pages. Add Redis for distributed session. Consider CQRS read models for complex reporting. |

### Scaling Priorities

1. **First bottleneck (likely): Large table queries.** `ERApplicationTemplateFieldData` (5.8M rows) and `ERCandidate` (1.3M rows) will be the first pain points. Mitigation: Ensure EF Core projections (`.Select()`) instead of full entity loads. Use `ProjectToDto()` pattern. Add appropriate indexes.
2. **Second bottleneck: YARP proxy hop.** Every unmigrated request adds latency. Mitigation: Prioritize migrating high-traffic pages first. The proxy overhead disappears as migration completes.
3. **Third bottleneck: Blazor circuit memory.** Each connected user holds server memory. Mitigation: Use SSR (Static Server Rendering) for read-only pages. Reserve Interactive Server mode for pages that need real-time interaction.

## Anti-Patterns

### Anti-Pattern 1: Leaking IQueryable Outside Repositories

**What people do:** Return `IQueryable<T>` from repository methods, letting Application/UI layers compose queries.
**Why it's wrong:** Breaks the abstraction boundary. Application layer becomes coupled to EF Core's query provider. Queries become untestable without a real database. LINQ-to-SQL translation failures surface in the wrong layer.
**Do this instead:** Repositories return `Task<IReadOnlyList<T>>`, `Task<T?>`, or `Task<PaginatedList<T>>`. All query composition (filtering, sorting, paging) happens inside repository methods or via the Specification pattern. The `ProjectToDto()` extension is the one exception -- it returns `IQueryable<TDto>` but is called within repository/infrastructure methods only.

### Anti-Pattern 2: Fat Blazor Components

**What people do:** Put data access, business logic, and validation directly in `.razor` component code-behind.
**Why it's wrong:** Components become untestable. Logic cannot be reused. Violates separation of concerns. Makes migration to different UI frameworks impossible.
**Do this instead:** Blazor components call Application layer services or handlers. Components are thin: they manage UI state, call services, and render. All logic lives in Application/Domain layers. Components inject `IDbContextFactory` or Application services, never `DbContext` directly.

### Anti-Pattern 3: Skipping Tenant Validation on Writes

**What people do:** Rely solely on global query filters (which only affect reads) and forget to validate tenant on CREATE/UPDATE operations.
**Why it's wrong:** A user could potentially create or modify entities belonging to another tenant. Global query filters are read-side only.
**Do this instead:** Implement a `SaveChangesInterceptor` that validates every entity being added or modified has the correct `ClientId`. Additionally, Application layer use cases should explicitly set `ClientId` from `ITenantProvider` on new entities.

### Anti-Pattern 4: Using AutoMapper or Reflection-Based Mapping

**What people do:** Add AutoMapper for convenience, creating implicit mappings between entities and DTOs.
**Why it's wrong for this project:** AutoMapper became commercial in April 2025. Reflection-based mapping hides mismatches until runtime. Over a 10+ year lifespan, mapping breakages from renamed properties cause silent bugs. The "convenience" savings are minimal compared to the debugging cost.
**Do this instead:** Manual extension method mapping. Compile-time safe. Debuggable. Zero external dependency. See Pattern 2 above.

### Anti-Pattern 5: Sharing DbContext Across Blazor Operations

**What people do:** Inject `DbContext` as scoped and reuse it across multiple user interactions within a Blazor circuit.
**Why it's wrong:** In Blazor Server, a "scope" lives for the entire circuit duration (the user's session). A long-lived DbContext accumulates tracked entities, causes memory leaks, and produces stale data.
**Do this instead:** Use `IDbContextFactory<SignaturDbContext>`. Create a new DbContext per operation (per button click, per data load). Dispose it immediately via `using`. This is the Microsoft-documented pattern for Blazor + EF Core.

## Integration Points

### External Services

| Service | Integration Pattern | Notes |
|---------|---------------------|-------|
| Legacy WebForms App | YARP reverse proxy + System.Web Adapters | ASP.NET Core app sits in front. Unmatched routes forwarded. Session/auth shared via remote adapters. |
| SQL Server (SignaturAnnoncePortal) | EF Core (database-first on existing schema) | No migrations -- existing schema is read-only from EF Core's perspective. Use `modelBuilder.ApplyConfigurationsFromAssembly()` with `IEntityTypeConfiguration<T>` to map entities to existing tables. |
| Jobnet / LinkedIn / Naturejobs | Infrastructure service implementations | External job board integrations. Implement as Infrastructure services behind Domain interfaces. |
| KMD Opus / Silkeborg Data | Infrastructure service implementations | Danish HR system integrations. Keep behind interfaces for testability. |
| MitID / Kombit | Authentication infrastructure | Danish identity providers. Integrate at the Web layer (authentication middleware). |

### Internal Boundaries

| Boundary | Communication | Notes |
|----------|---------------|-------|
| Web <-> Application | Direct method calls via DI | Application services/handlers injected into Blazor components. No HTTP overhead. |
| Application <-> Domain | Direct object references | Application uses Domain entities and interfaces. Domain has no knowledge of Application. |
| Application <-> Infrastructure | Via interfaces (Dependency Inversion) | Application defines `IRepository<T>`, `IUnitOfWork`. Infrastructure implements them. Wired via DI in Web project. |
| New App <-> Legacy App | HTTP via YARP + System.Web Adapters | Network boundary. Keep shared state minimal (session keys, auth claims). |

## Build Order (Foundation -> Features)

The dependency graph dictates what must be built first. This is critical for roadmap phasing.

```
Phase 1: Foundation (must be first -- everything depends on this)
    |
    +-- SharedKernel (BaseEntity, ITenantEntity, Result<T>)
    +-- Domain (core interfaces: IRepository, IUnitOfWork, ITenantProvider)
    +-- Infrastructure skeleton (DbContext, entity configurations for existing schema)
    +-- Web skeleton (Program.cs with YARP fallback to legacy app)
    +-- System.Web Adapters (session + auth sharing)
    |
    v -- Foundation complete: legacy app proxied, new app compiles, DB connected --
    |
Phase 2: Vertical Slice (prove the architecture with one feature end-to-end)
    |
    +-- Pick one domain: e.g., Activity/Job Posting list page
    +-- Domain entities for that slice
    +-- Repository + UnitOfWork implementation for that slice
    +-- Application use case (query handler + DTO + mapping)
    +-- Blazor page with MudBlazor data grid
    +-- Multi-tenancy enforcement verified end-to-end
    |
    v -- Architecture validated, patterns established --
    |
Phase 3+: Feature Migration (parallel work possible)
    |
    +-- Migrate features by domain area
    +-- Each feature follows established patterns
    +-- Progressively fewer routes fall through to YARP
    |
    v -- Migration complete when YARP forwards zero routes --
```

**Build dependency chain:**

| Component | Depends On | Blocks |
|-----------|-----------|--------|
| SharedKernel | Nothing | Domain, Infrastructure |
| Domain | SharedKernel | Application, Infrastructure |
| Infrastructure (EF Core) | Domain, SharedKernel | Application (at runtime), Web |
| Application | Domain | Web (Blazor components) |
| Web (YARP + Blazor host) | All projects | Nothing (it's the top) |
| System.Web Adapters config | Web project existing | Feature migration (session/auth sharing) |
| Multi-tenancy (global filters) | Infrastructure (DbContext) | All data-accessing features |
| First Blazor page | All of the above | Proves the architecture; unblocks parallel feature work |

**Critical path:** SharedKernel -> Domain interfaces -> Infrastructure DbContext (with tenant filters) -> Web host with YARP -> First vertical slice. This critical path must complete before any feature migration can begin.

## Sources

### Official Microsoft Documentation (HIGH confidence)
- [Clean Architecture - Common Web Application Architectures](https://learn.microsoft.com/dotnet/architecture/modern-web-apps-azure/common-web-application-architectures#clean-architecture)
- [ASP.NET Core Blazor with Entity Framework Core](https://learn.microsoft.com/en-us/aspnet/core/blazor/blazor-ef-core?view=aspnetcore-10.0)
- [EF Core Multi-tenancy](https://learn.microsoft.com/ef/core/miscellaneous/multitenancy)
- [EF Core Global Query Filters](https://learn.microsoft.com/ef/core/querying/filters)
- [EF Core 10 Named Query Filters](https://learn.microsoft.com/ef/core/what-is-new/ef-core-10.0/whatsnew#named-query-filters)
- [YARP Getting Started](https://learn.microsoft.com/aspnet/core/fundamentals/servers/yarp/getting-started?view=aspnetcore-10.0)
- [YARP Configuration Files](https://learn.microsoft.com/aspnet/core/fundamentals/servers/yarp/config-files?view=aspnetcore-10.0)
- [Incremental ASP.NET to ASP.NET Core Migration](https://learn.microsoft.com/aspnet/core/migration/fx-to-core/start?view=aspnetcore-10.0)
- [Remote App Setup (System.Web Adapters)](https://learn.microsoft.com/aspnet/core/migration/fx-to-core/inc/remote-app-setup?view=aspnetcore-10.0)
- [Remote Authentication (System.Web Adapters)](https://learn.microsoft.com/aspnet/core/migration/fx-to-core/areas/authentication?view=aspnetcore-10.0#remote-authentication)
- [Remote Session State (System.Web Adapters)](https://learn.microsoft.com/aspnet/core/migration/fx-to-core/areas/session?view=aspnetcore-10.0#remote-app-session-state)
- [Implementing Infrastructure Persistence with EF Core](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/infrastructure-persistence-layer-implementation-entity-framework-core)

### Reference Implementations (HIGH confidence)
- [ardalis/CleanArchitecture (.NET 10 template)](https://github.com/ardalis/CleanArchitecture) -- 4-project structure: Core, UseCases, Infrastructure, Web
- [jasontaylordev/CleanArchitecture](https://github.com/jasontaylordev/CleanArchitecture) -- Domain, Application, Infrastructure, Web structure
- [neozhu/CleanArchitectureWithBlazorServer](https://github.com/neozhu/CleanArchitectureWithBlazorServer) -- Blazor Server-specific Clean Architecture
- [thecodewrapper/CH.CleanArchitectureBlazor](https://github.com/thecodewrapper/CH.CleanArchitectureBlazor) -- .NET 10 Blazor Clean Architecture

### Community Sources (MEDIUM confidence)
- [Trailhead Technology -- Upgrading WebForms to Blazor with YARP](https://trailheadtechnology.com/upgrading-an-asp-net-web-forms-app-to-blazor-incrementally-with-yarp/)
- [Jimmy Bogard -- Tales from the .NET Migration Trenches: Session State](https://www.jimmybogard.com/tales-from-the-net-migration-trenches-session-state/)
- [Milan Jovanovic -- Multi-Tenant Applications With EF Core](https://www.milanjovanovic.tech/blog/multi-tenant-applications-with-ef-core)
- [Jeremy Likness -- Multi-tenancy with EF Core in Blazor Server Apps](https://blog.jeremylikness.com/blog/multitenancy-with-ef-core-in-blazor-server-apps/)
- [Shahed -- How to Map Objects in .NET in 2026](https://shahedbd.medium.com/how-to-map-objects-in-net-in-2026-automapper-vs-mapster-vs-manual-b1cef7827ff5)

---
*Architecture research for: Signatur ATS Platform Modernization*
*Researched: 2026-02-13*
