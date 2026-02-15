# Phase 3: Core Read Views - Research

**Researched:** 2026-02-15
**Domain:** Blazor Server + MudBlazor DataGrid + EF Core read operations
**Confidence:** HIGH

## Summary

Phase 3 implements read-only views for recruitment activities and candidate applications using MudBlazor's MudDataGrid with server-side data operations. The architecture combines Blazor Server with EF Core via IDbContextFactory for circuit-safe database access, implementing pagination, filtering, sorting, and file downloads from database BLOBs.

**Primary recommendation:** Use MudDataGrid with ServerData callback for all list views, implement server-side pagination/filtering/sorting at the repository layer, leverage EF Core global query filters for tenant isolation and permission-based access, and use DotNetStreamReference for file downloads under 250 MB.

**Key architectural decisions:**
- MudDataGrid is the standard for data grids in MudBlazor (v7+), replacing the older MudTable component
- IDbContextFactory ensures circuit-safe DbContext usage in Blazor Server
- Server-side data operations are essential for performance with large datasets (target: < 2s load time)
- File downloads from database use JavaScript interop with DotNetStreamReference for files < 250 MB
- Global query filters provide automatic tenant isolation and permission-based filtering

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| MudBlazor | 7.8.0+ | UI component library with DataGrid | Official Blazor component library with mature DataGrid, active development, excellent documentation |
| EF Core | 10.0 | ORM for database access | .NET 10 includes named query filters, compiled queries, improved performance |
| IDbContextFactory | Built-in | DbContext factory for Blazor Server | Microsoft-recommended pattern for circuit-safe EF Core usage in Blazor Server |
| System.Linq.Dynamic.Core | 1.4.0+ | Dynamic LINQ for filter/sort | Enables dynamic query building from MudDataGrid filter/sort definitions |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| AutoMapper | 13.0+ | DTO mapping | Map entities to DTOs for view models |
| FluentValidation | 11.9+ | Input validation | Validate filter inputs, search criteria |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| MudDataGrid | Radzen DataGrid | Radzen has good performance but MudBlazor has better Material Design adherence and ecosystem integration |
| MudDataGrid | Telerik Blazor Grid | Telerik requires commercial license, more features but higher cost |
| ServerData callback | Client-side pagination | Client-side only works for small datasets (< 1000 rows), violates 2s load time requirement |
| IDbContextFactory | Scoped DbContext | Scoped DbContext causes thread safety issues in Blazor Server circuits |

**Installation:**
```bash
# MudBlazor already installed in Phase 1
# EF Core already configured in Phase 1
# Add dynamic LINQ for flexible filtering
dotnet add src/SignaturPortal.Application package System.Linq.Dynamic.Core
```

## Architecture Patterns

### Recommended Project Structure
```
src/
├── SignaturPortal.Web/
│   ├── Pages/
│   │   └── Activities/
│   │       ├── ActivityList.razor         # Main activity list view with MudDataGrid
│   │       ├── ActivityDetail.razor       # Activity detail view
│   │       └── ActivityDetail.razor.cs    # Code-behind for separation of concerns
│   ├── Components/
│   │   ├── ApplicationList.razor          # Reusable application list component
│   │   └── FileDownload.razor             # Reusable file download component
│   └── wwwroot/
│       └── js/
│           └── fileDownload.js            # JavaScript for file download interop
├── SignaturPortal.Application/
│   ├── Services/
│   │   ├── ActivityService.cs             # Activity business logic
│   │   ├── ApplicationService.cs          # Application business logic
│   │   └── FileService.cs                 # File download/streaming logic
│   ├── DTOs/
│   │   ├── ActivityListDto.cs             # Flat DTO for grid display
│   │   ├── ActivityDetailDto.cs           # Detailed DTO with navigation props
│   │   └── GridRequest.cs                 # Generic grid query wrapper
│   └── Interfaces/
│       └── IActivityService.cs
├── SignaturPortal.Infrastructure/
│   └── Repositories/
│       ├── ActivityRepository.cs          # Activity-specific queries
│       └── Extensions/
│           └── QueryableExtensions.cs     # Reusable filter/sort/page logic
```

### Pattern 1: Server-Side DataGrid with GridRequest/GridResponse

**What:** Standardized request/response pattern for all MudDataGrid ServerData callbacks
**When to use:** Every MudDataGrid in the application
**Benefits:** Consistent API, reusable filtering logic, testable

**Example:**
```csharp
// Source: MudBlazor official docs + Clean Architecture pattern
// https://mudblazor.com/components/datagrid

// Application layer - generic request wrapper
public class GridRequest
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public List<SortDefinition> Sorts { get; set; } = new();
    public List<FilterDefinition> Filters { get; set; } = new();
}

public class GridResponse<T>
{
    public List<T> Items { get; set; } = new();
    public int TotalCount { get; set; }
}

// Application service
public interface IActivityService
{
    Task<GridResponse<ActivityListDto>> GetActivitiesAsync(GridRequest request, CancellationToken ct);
}

// Blazor component
@inject IActivityService ActivityService

<MudDataGrid T="ActivityListDto"
             ServerData="LoadServerData"
             Filterable="true"
             Sortable="true">
    <Columns>
        <PropertyColumn Property="x => x.Headline" Title="Activity" />
        <PropertyColumn Property="x => x.ApplicationDeadline" Title="Deadline" />
        <PropertyColumn Property="x => x.StatusName" Title="Status" />
    </Columns>
</MudDataGrid>

@code {
    private async Task<GridData<ActivityListDto>> LoadServerData(GridState<ActivityListDto> state)
    {
        var request = new GridRequest
        {
            Page = state.Page,
            PageSize = state.PageSize,
            Sorts = state.SortDefinitions.ToList(),
            Filters = state.FilterDefinitions.ToList()
        };

        var response = await ActivityService.GetActivitiesAsync(request, CancellationToken.None);

        return new GridData<ActivityListDto>
        {
            Items = response.Items,
            TotalItems = response.TotalCount
        };
    }
}
```

### Pattern 2: IDbContextFactory Scoped to Component Method

**What:** Create DbContext per operation using IDbContextFactory, dispose immediately
**When to use:** All database operations in services
**Benefits:** Thread-safe, prevents circuit lifetime issues, explicit scope

**Example:**
```csharp
// Source: Microsoft official docs
// https://learn.microsoft.com/en-us/aspnet/core/blazor/blazor-ef-core

// Service registration (Program.cs)
builder.Services.AddDbContextFactory<SignaturDbContext>(options =>
    options.UseSqlServer(connectionString));

// Service implementation
public class ActivityService : IActivityService
{
    private readonly IDbContextFactory<SignaturDbContext> _contextFactory;

    public ActivityService(IDbContextFactory<SignaturDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<GridResponse<ActivityListDto>> GetActivitiesAsync(
        GridRequest request,
        CancellationToken ct)
    {
        // Create fresh DbContext for this operation
        using var context = await _contextFactory.CreateDbContextAsync(ct);

        var query = context.Eractivity
            .Include(a => a.Client)
            .Where(a => !a.IsCleaned); // Base filter

        // Apply filters, sorts, pagination
        var totalCount = await query.CountAsync(ct);
        var items = await query
            .ApplyFilters(request.Filters)
            .ApplySorts(request.Sorts)
            .Skip(request.Page * request.PageSize)
            .Take(request.PageSize)
            .Select(a => new ActivityListDto
            {
                EractivityId = a.EractivityId,
                Headline = a.Headline,
                ApplicationDeadline = a.ApplicationDeadline,
                // ... other properties
            })
            .ToListAsync(ct);

        return new GridResponse<ActivityListDto>
        {
            Items = items,
            TotalCount = totalCount
        };
    }
    // DbContext disposed here automatically
}
```

### Pattern 3: Global Query Filters for Tenant Isolation

**What:** EF Core global query filters automatically scope all queries to current tenant and user permissions
**When to use:** All entities with ClientId, SiteId, or permission requirements
**Benefits:** Security by default, can't accidentally leak tenant data, DRY principle

**Example:**
```csharp
// Source: EF Core documentation
// https://learn.microsoft.com/en-us/ef/core/querying/filters

// DbContext configuration
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    // Tenant isolation filter
    modelBuilder.Entity<Eractivity>()
        .HasQueryFilter("TenantFilter",
            a => a.ClientId == _userSessionContext.ClientId);

    // Soft delete filter
    modelBuilder.Entity<Eractivity>()
        .HasQueryFilter("SoftDeleteFilter",
            a => !a.IsCleaned);

    // Permission-based filter (applied in repository layer for flexibility)
    // See Pattern 4
}

// To explicitly bypass filter (admin scenarios)
var allActivities = await context.Eractivity
    .IgnoreQueryFilters(new[] { "TenantFilter" })
    .ToListAsync();
```

### Pattern 4: Permission-Based Filtering with Expression Composition

**What:** Build dynamic LINQ expressions for permission checks, compose with other filters
**When to use:** Entities requiring row-level permission checks beyond tenant isolation
**Benefits:** Flexible, testable, maintains query efficiency

**Example:**
```csharp
// Repository extension method
public static IQueryable<Eractivity> ApplyUserPermissions(
    this IQueryable<Eractivity> query,
    IUserSessionContext userContext,
    IPermissionService permissionService)
{
    var userId = userContext.UserId;

    // Build permission predicate based on user permissions
    // "View own activities" vs "View all activities in client"
    if (permissionService.HasPermission(ERecruitmentPermission.ViewAllActivities))
    {
        // Already scoped to client by global filter
        return query;
    }
    else if (permissionService.HasPermission(ERecruitmentPermission.ViewOwnActivities))
    {
        // Further restrict to activities where user is responsible or creator
        return query.Where(a =>
            a.Responsible == userId ||
            a.CreatedBy == userId);
    }
    else
    {
        // No permission - return empty set
        return query.Where(a => false);
    }
}

// Usage in service
var query = context.Eractivity
    .ApplyUserPermissions(userContext, permissionService)
    .ApplyFilters(request.Filters);
```

### Pattern 5: File Download from Database BLOB

**What:** Stream file from byte array/database to browser using JavaScript interop
**When to use:** CV, application letters, attachments stored in database
**File size limit:** < 250 MB (SignalR limitations)

**Example:**
```csharp
// Source: Microsoft Blazor file download documentation
// https://learn.microsoft.com/en-us/aspnet/core/blazor/file-downloads

// JavaScript (wwwroot/js/fileDownload.js)
window.downloadFileFromStream = async (fileName, contentStreamReference) => {
    const arrayBuffer = await contentStreamReference.arrayBuffer();
    const blob = new Blob([arrayBuffer]);
    const url = URL.createObjectURL(blob);
    const anchorElement = document.createElement('a');
    anchorElement.href = url;
    anchorElement.download = fileName ?? '';
    anchorElement.click();
    anchorElement.remove();
    URL.revokeObjectURL(url); // Critical: prevent memory leak
}

// Service
public class FileService
{
    private readonly IDbContextFactory<SignaturDbContext> _contextFactory;

    public async Task<Stream> GetCandidateCvAsync(int candidateId, CancellationToken ct)
    {
        using var context = await _contextFactory.CreateDbContextAsync(ct);

        // Assuming CV stored in related table
        var cvData = await context.Ercandidate
            .Where(c => c.ErcandidateId == candidateId)
            .Select(c => c.CvFileData) // byte[] or similar
            .FirstOrDefaultAsync(ct);

        if (cvData == null)
            throw new FileNotFoundException("CV not found");

        return new MemoryStream(cvData);
    }
}

// Blazor component
@inject IJSRuntime JS
@inject IFileService FileService

<MudIconButton Icon="@Icons.Material.Filled.Download"
               OnClick="@(() => DownloadCvAsync(candidate.Id))" />

@code {
    private async Task DownloadCvAsync(int candidateId)
    {
        try
        {
            var fileStream = await FileService.GetCandidateCvAsync(candidateId, CancellationToken.None);
            var fileName = $"CV_{candidateId}.pdf";

            using var streamRef = new DotNetStreamReference(stream: fileStream);
            await JS.InvokeVoidAsync("downloadFileFromStream", fileName, streamRef);
        }
        catch (Exception ex)
        {
            // Show error snackbar
            Snackbar.Add($"Failed to download CV: {ex.Message}", Severity.Error);
        }
    }
}
```

### Pattern 6: Avoiding N+1 Queries with Projection

**What:** Use Select projection instead of Include when only displaying flat data
**When to use:** Grid views with navigation properties
**Benefits:** Avoids cartesian explosion, reduces data transfer, improves performance

**Example:**
```csharp
// BAD: Using Include for grid data (N+1 or cartesian explosion)
var activities = await context.Eractivity
    .Include(a => a.Client)
    .Include(a => a.Ercandidates) // Cartesian explosion!
    .ToListAsync();

// GOOD: Project to DTO with only needed data
var activities = await context.Eractivity
    .Select(a => new ActivityListDto
    {
        EractivityId = a.EractivityId,
        Headline = a.Headline,
        ClientName = a.Client.Name, // Single join, no cartesian
        CandidateCount = a.Ercandidates.Count() // Aggregated, not loaded
    })
    .ToListAsync();

// For detail views with multiple collections, use AsSplitQuery()
var activityDetail = await context.Eractivity
    .Include(a => a.Ercandidates)
    .Include(a => a.HiringTeamMembers)
    .AsSplitQuery() // Generates separate queries for each collection
    .FirstOrDefaultAsync(a => a.EractivityId == id);
```

### Anti-Patterns to Avoid

- **Loading entire table to client:** Never use `.ToListAsync()` before filtering/paging - always filter on server
- **Scoped DbContext in Blazor Server:** Causes thread safety issues and memory leaks in circuits
- **Ignoring global query filters without justification:** Always document why filter is bypassed
- **Manual string building for dynamic filters:** Use expression trees or System.Linq.Dynamic.Core instead
- **Returning entities directly to Blazor components:** Always project to DTOs to avoid serialization/tracking issues

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Data grid with pagination | Custom table component | MudDataGrid with ServerData | Sorting, filtering, pagination, column resizing, grouping all built-in and tested |
| Dynamic filtering/sorting | Manual SQL string building | System.Linq.Dynamic.Core or expression builders | SQL injection risks, type safety, maintainability |
| File download | Custom byte array handlers | DotNetStreamReference + JS interop | Memory management, browser compatibility, cleanup handled |
| Multi-tenancy isolation | Manual WHERE clauses | EF Core global query filters | Can't forget to filter, applied automatically, testable |
| Permission checks | Inline if statements | IAuthorizationService + global filters | Centralized logic, auditable, consistent |
| DTO mapping | Manual property copying | AutoMapper | Reduces boilerplate, handles nested objects, profile-based |

**Key insight:** MudBlazor and EF Core have mature solutions for common data grid and data access patterns. Custom solutions introduce bugs, security risks, and maintenance burden. The complexity is in correctly configuring these tools, not rebuilding them.

## Common Pitfalls

### Pitfall 1: Forgetting to Apply Tenant/Permission Filters in Queries
**What goes wrong:** Users see data from other tenants or activities they don't have permission to access
**Why it happens:** Global query filters only apply to base entity queries, not to joins or included entities unless properly configured
**How to avoid:**
- Configure global query filters in OnModelCreating for all tenant-scoped entities
- Use repository extension methods that consistently apply permission logic
- Write integration tests that verify tenant isolation
**Warning signs:**
- Test data from different clients appearing in lists
- Count mismatches between what user should see vs. what query returns

### Pitfall 2: Memory Leaks from Long-Lived DbContext
**What goes wrong:** Blazor Server circuit memory grows unbounded, eventual OutOfMemoryException
**Why it happens:** Scoped DbContext lives for entire circuit lifetime, tracks all entities ever loaded
**How to avoid:**
- ALWAYS use IDbContextFactory, create DbContext per operation
- Use `using` statement or `using var` to ensure disposal
- Never inject DbContext directly into components or scoped services
**Warning signs:**
- Memory usage grows over time in Blazor Server app
- Stale data appearing in UI
- Entity tracking conflicts

### Pitfall 3: N+1 Queries with MudDataGrid + Include
**What goes wrong:** Loading activity list with included candidates generates 1 query for activities + N queries for each activity's candidates
**Why it happens:** EF Core's lazy loading or misuse of Include with multiple collections
**How to avoid:**
- Use Select projection to DTOs instead of Include for list views
- Use AsSplitQuery() when loading multiple collections
- Enable SQL logging in development to detect N+1
**Warning signs:**
- Grid loading takes > 2 seconds despite small page size
- SQL profiler shows hundreds of queries for single page load
- EF Core tracking a large number of entities

### Pitfall 4: SignalR Buffer Size Exceeded on File Download
**What goes wrong:** File download fails with "Maximum message size exceeded" error
**Why it happens:** Blazor Server uses SignalR with 32 KB default message size, large files exceed limit
**How to avoid:**
- Increase SignalR MaximumReceiveMessageSize in Program.cs
- Limit file downloads to < 250 MB via streaming
- For larger files, use URL-based download pattern instead
- Chunk large files if streaming
**Warning signs:**
- Downloads work for small files but fail for large PDFs/documents
- SignalR connection errors during download

**Program.cs configuration:**
```csharp
builder.Services.AddServerSideBlazor()
    .AddHubOptions(options =>
    {
        options.MaximumReceiveMessageSize = 10 * 1024 * 1024; // 10 MB
    });
```

### Pitfall 5: MudDataGrid ServerData Not Refreshing After External State Change
**What goes wrong:** Grid doesn't reload after creating/editing/deleting data in a dialog
**Why it happens:** MudDataGrid doesn't automatically detect external state changes
**How to avoid:**
- Call `await dataGrid.ReloadServerData()` after modifying data
- Store `@ref="dataGrid"` on MudDataGrid component
- Trigger reload in dialog OnClose callback
**Warning signs:**
- User creates new activity but doesn't see it in list until page refresh
- Deleting item doesn't remove it from grid

**Example:**
```csharp
<MudDataGrid @ref="dataGrid" T="ActivityListDto" ServerData="LoadServerData" />

@code {
    private MudDataGrid<ActivityListDto> dataGrid;

    private async Task OnActivityCreated()
    {
        await dataGrid.ReloadServerData();
    }
}
```

### Pitfall 6: Global Query Filter Applied to Navigation Causes Row Filtering
**What goes wrong:** Loading Activity with included Candidates filters out candidates that have IsCleaned = true, even though you want to see all candidates for that activity
**Why it happens:** Global query filters apply to navigations and joins, not just root queries
**How to avoid:**
- Use IgnoreQueryFilters() when intentionally loading filtered navigation data
- Configure filters carefully - consider if filter should apply to navigations
- Use projection instead of Include when you need unfiltered child data
**Warning signs:**
- Activity detail shows fewer candidates than expected
- Count of candidates in list view doesn't match detail view

### Pitfall 7: Missing await on IDbContextFactory.CreateDbContextAsync
**What goes wrong:** DbContext not properly initialized, random failures
**Why it happens:** CreateDbContextAsync is async but developers use CreateDbContext (sync)
**How to avoid:**
- Always use `await _contextFactory.CreateDbContextAsync(ct)`
- Pass CancellationToken for proper cancellation support
- Enable nullable reference types to catch null returns
**Warning signs:**
- Intermittent DbContext initialization errors
- Connection pool exhaustion

## Code Examples

Verified patterns from official sources:

### MudDataGrid with Server-Side Pagination
```csharp
// Source: https://mudblazor.com/components/datagrid
<MudDataGrid T="ActivityListDto"
             ServerData="@(new Func<GridState<ActivityListDto>, Task<GridData<ActivityListDto>>>(LoadServerData))"
             Filterable="true"
             FilterMode="DataGridFilterMode.ColumnFilterRow"
             Sortable="true"
             SortMode="SortMode.Multiple"
             Pageable="true"
             RowsPerPage="25">
    <Columns>
        <PropertyColumn Property="x => x.Headline" Title="Activity" Filterable="true" Sortable="true" />
        <PropertyColumn Property="x => x.JournalNo" Title="Journal No" />
        <PropertyColumn Property="x => x.ApplicationDeadline" Title="Deadline" Format="yyyy-MM-dd">
            <FilterTemplate>
                <MudDatePicker @bind-Date="context.FilterContext.FilterDefinition.Value" />
            </FilterTemplate>
        </PropertyColumn>
        <PropertyColumn Property="x => x.StatusName" Title="Status" />
        <TemplateColumn Title="Candidates" Sortable="false">
            <CellTemplate>
                <MudText>@context.Item.CandidateCount</MudText>
            </CellTemplate>
        </TemplateColumn>
        <TemplateColumn Title="Actions" Sortable="false" Filterable="false">
            <CellTemplate>
                <MudIconButton Icon="@Icons.Material.Filled.Visibility"
                               OnClick="@(() => NavigateToDetail(context.Item.EractivityId))" />
            </CellTemplate>
        </TemplateColumn>
    </Columns>
    <PagerContent>
        <MudDataGridPager T="ActivityListDto" PageSizeOptions="new int[] { 10, 25, 50, 100 }" />
    </PagerContent>
</MudDataGrid>

@code {
    private async Task<GridData<ActivityListDto>> LoadServerData(GridState<ActivityListDto> state)
    {
        var request = new GridRequest
        {
            Page = state.Page,
            PageSize = state.PageSize,
            Sorts = state.SortDefinitions.Select(s => new SortDefinition
            {
                PropertyName = s.SortBy,
                Descending = s.Descending
            }).ToList(),
            Filters = state.FilterDefinitions.Select(f => new FilterDefinition
            {
                PropertyName = f.FieldName,
                Operator = f.Operator,
                Value = f.Value
            }).ToList()
        };

        var response = await ActivityService.GetActivitiesAsync(request, CancellationToken.None);

        return new GridData<ActivityListDto>
        {
            Items = response.Items,
            TotalItems = response.TotalCount
        };
    }
}
```

### IDbContextFactory in Service Layer
```csharp
// Source: https://learn.microsoft.com/en-us/aspnet/core/blazor/blazor-ef-core
public class ActivityService : IActivityService
{
    private readonly IDbContextFactory<SignaturDbContext> _contextFactory;
    private readonly IUserSessionContext _userSessionContext;
    private readonly IPermissionService _permissionService;

    public ActivityService(
        IDbContextFactory<SignaturDbContext> contextFactory,
        IUserSessionContext userSessionContext,
        IPermissionService permissionService)
    {
        _contextFactory = contextFactory;
        _userSessionContext = userSessionContext;
        _permissionService = permissionService;
    }

    public async Task<GridResponse<ActivityListDto>> GetActivitiesAsync(
        GridRequest request,
        CancellationToken ct)
    {
        using var context = await _contextFactory.CreateDbContextAsync(ct);

        var query = context.Eractivity
            .Where(a => !a.IsCleaned) // Base filter
            .ApplyUserPermissions(_userSessionContext, _permissionService);

        // Get total count before pagination
        var totalCount = await query.CountAsync(ct);

        // Apply filtering, sorting, pagination
        var items = await query
            .ApplyFilters(request.Filters)
            .ApplySorts(request.Sorts)
            .Skip(request.Page * request.PageSize)
            .Take(request.PageSize)
            .Select(a => new ActivityListDto
            {
                EractivityId = a.EractivityId,
                Headline = a.Headline,
                Jobtitle = a.Jobtitle,
                JournalNo = a.JournalNo,
                ApplicationDeadline = a.ApplicationDeadline,
                StatusName = GetStatusName(a.EractivityStatusId),
                ClientName = a.Client.Name,
                CandidateCount = a.Ercandidates.Count(c => !c.IsDeleted)
            })
            .ToListAsync(ct);

        return new GridResponse<ActivityListDto>
        {
            Items = items,
            TotalCount = totalCount
        };
    }
}
```

### Dynamic Filter Application
```csharp
// Repository extension for dynamic filtering
public static IQueryable<T> ApplyFilters<T>(
    this IQueryable<T> query,
    List<FilterDefinition> filters)
{
    foreach (var filter in filters)
    {
        if (filter.Value == null) continue;

        // Use System.Linq.Dynamic.Core for safe dynamic filtering
        query = filter.Operator switch
        {
            "contains" => query.Where($"{filter.PropertyName}.Contains(@0)", filter.Value),
            "equals" => query.Where($"{filter.PropertyName} == @0", filter.Value),
            "startswith" => query.Where($"{filter.PropertyName}.StartsWith(@0)", filter.Value),
            "endswith" => query.Where($"{filter.PropertyName}.EndsWith(@0)", filter.Value),
            _ => query
        };
    }
    return query;
}

public static IQueryable<T> ApplySorts<T>(
    this IQueryable<T> query,
    List<SortDefinition> sorts)
{
    if (!sorts.Any()) return query;

    var orderBy = string.Join(", ", sorts.Select(s =>
        $"{s.PropertyName} {(s.Descending ? "desc" : "asc")}"));

    return query.OrderBy(orderBy);
}
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| MudTable | MudDataGrid | MudBlazor v6+ | DataGrid has better performance, built-in filtering, grouping |
| Scoped DbContext | IDbContextFactory | EF Core 5.0 / .NET 5 | Resolves Blazor Server circuit lifetime issues |
| Single global filter | Named query filters | EF Core 10.0 | Can define multiple filters per entity, disable selectively |
| Manual DTO mapping | AutoMapper / Mapperly | Always available | Code generation (Mapperly) faster than reflection (AutoMapper) |
| Include for all scenarios | Projection + AsSplitQuery | EF Core 5.0+ | Avoids cartesian explosion, reduces data transfer |
| Manual pagination logic | Skip/Take with CountAsync | Standard LINQ | Consistent, tested, works with all providers |

**Deprecated/outdated:**
- **MudTable:** Replaced by MudDataGrid in MudBlazor v6+. MudTable still works but DataGrid is recommended for new projects
- **AddDbContext for Blazor Server:** Use AddDbContextFactory instead to avoid circuit lifetime issues
- **Single HasQueryFilter call:** Pre-EF 10, only one filter per entity. Now use named filters for multiple filters
- **Lazy loading in Blazor Server:** Causes N+1 queries and circuit memory issues. Use eager loading with Include or projection

## Open Questions

1. **File storage location (database vs filesystem)**
   - What we know: Legacy app stores some files in database (byte[]), some in filesystem
   - What's unclear: Which tables store file data, file size distribution, storage strategy
   - Recommendation: Phase 3 assumes database storage (based on ERCandidate structure), verify during implementation. Add filesystem support in Phase 4 if needed.

2. **Hiring team member data structure**
   - What we know: ELIST-09 requires displaying hiring team members
   - What's unclear: Table/entity name for team members, relationship to ERActivity
   - Recommendation: Investigate during implementation. Likely a junction table (ERActivityMember or similar) with User reference.

3. **Permission granularity for activities**
   - What we know: ELIST-06 requires permission-based filtering
   - What's unclear: Full permission matrix (role-based vs activity-based, view own vs view all vs view team)
   - Recommendation: Start with simple "view own" vs "view all" based on Responsible/CreatedBy. Refine in Phase 4 based on legacy behavior.

4. **Filter persistence across page navigation**
   - What we know: Users will want to return to filtered list after viewing detail
   - What's unclear: Should filters persist in browser session, local storage, or user preferences?
   - Recommendation: Phase 3 uses component state (filters reset on navigation). Add persistence in Phase 5 (UX enhancements) if users request it.

## Sources

### Primary (HIGH confidence)
- [MudBlazor DataGrid Official Documentation](https://mudblazor.com/components/datagrid) - Component API, ServerData pattern, filtering/sorting configuration
- [ASP.NET Core Blazor with EF Core - Microsoft Learn](https://learn.microsoft.com/en-us/aspnet/core/blazor/blazor-ef-core?view=aspnetcore-10.0) - IDbContextFactory pattern, circuit safety, best practices
- [EF Core Global Query Filters - Microsoft Learn](https://learn.microsoft.com/en-us/ef/core/querying/filters) - Multi-tenancy patterns, named filters (.NET 10), filter configuration
- [ASP.NET Core Blazor File Downloads - Microsoft Learn](https://learn.microsoft.com/en-us/aspnet/core/blazor/file-downloads?view=aspnetcore-10.0) - DotNetStreamReference pattern, file size limits, JavaScript interop
- [EF Core Multi-tenancy - Microsoft Learn](https://learn.microsoft.com/en-us/ef/core/miscellaneous/multitenancy) - Tenant isolation strategies, global filter patterns

### Secondary (MEDIUM confidence)
- [Using EF Core Like a Pro - Medium (Jan 2026)](https://medium.com/dotnet-new/using-ef-core-like-a-pro-performance-patterns-and-modern-features-in-net-8-10-63d458ba3064) - EF Core 10 performance features, compiled queries
- [Named Query Filters in EF 10 - Medium (Jan 2026)](https://medium.com/@sangheraajit/named-query-filters-in-ef-10-multiple-filters-per-entity-796401825f6d) - Multiple filters per entity, selective disabling
- [End-to-End Server-Side Paging, Sorting, and Filtering in Blazor - Medium (Dec 2025)](https://medium.com/dotnet-new/end-to-end-server-side-paging-sorting-and-filtering-in-blazor-14078b147cc2) - Complete server-side data pattern
- [Solving the N+1 Problem in EF Core - Programming Pulse](https://programmingpulse.vercel.app/blog/solving-the-n1-problem-in-entity-framework-core) - Include/ThenInclude best practices, AsSplitQuery
- [Row Level Authorization with Entity Framework - OnCodeDesign](https://oncodedesign.com/blog/row-level-authorization/) - Permission-based filtering patterns

### Tertiary (LOW confidence - needs verification)
- [MudDataGrid ServerData GitHub Issues](https://github.com/MudBlazor/MudBlazor/issues/6623) - Community discussions on server-side patterns
- [MudDataGrid Sorting Discussion](https://github.com/MudBlazor/MudBlazor/discussions/7701) - Template column sorting challenges

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH - MudBlazor and EF Core patterns verified with official Microsoft documentation
- Architecture: HIGH - Patterns based on Clean Architecture + official Blazor/EF Core guidance
- Pitfalls: MEDIUM-HIGH - Combination of official docs (HIGH) and community experience (MEDIUM)
- File downloads: HIGH - Official Microsoft Blazor documentation with code examples
- Permission filtering: MEDIUM - Patterns exist but implementation details vary by requirements

**Research date:** 2026-02-15
**Valid until:** 2026-03-15 (30 days - Blazor/EF Core are stable, MudBlazor releases ~monthly)

**Next steps for planner:**
1. Create detailed task breakdown for Activity List (ELIST-01 to ELIST-10)
2. Create detailed task breakdown for Application Viewing (APP-01 to APP-05)
3. Define verification criteria for < 2s load time performance target
4. Identify which file storage patterns exist in legacy (database vs filesystem)
5. Define permission matrix for activity access control
