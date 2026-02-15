# Phase 04: Core Write Operations - Research

**Researched:** 2026-02-15
**Domain:** Blazor Server CRUD operations with EF Core, FluentValidation, and MudBlazor
**Confidence:** HIGH

## Summary

Phase 4 implements create, edit, and delete operations for recruitment activities in a Blazor Server application with a database-first EF Core approach. The research reveals a mature ecosystem with established patterns for form validation, concurrency handling, and audit logging. Key findings indicate that FluentValidation integration libraries have evolved significantly, with Blazilla emerging as the recommended successor to Blazored.FluentValidation. EF Core's optimistic concurrency with RowVersion provides battle-tested conflict detection, while MudBlazor offers robust form components with built-in validation support. The circuit resilience features in .NET 10 provide native state persistence capabilities that eliminate the need for third-party auto-save solutions.

**Primary recommendation:** Use Blazilla for FluentValidation integration, implement application-managed audit logging via SaveChanges override, leverage EF Core's native optimistic concurrency with RowVersion (if available in schema), and utilize .NET 10's persistent component state for circuit resilience rather than complex auto-save mechanisms.

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| FluentValidation | 11.x+ | Server-side validation | Industry standard for complex validation rules, type-safe, testable |
| Blazilla | Latest | FluentValidation-Blazor bridge | Successor to Blazored.FluentValidation, actively maintained, supports async validation |
| MudBlazor | 8.x+ (Phase 3) | Form UI components | Already in project, supports both EditForm and native MudForm validation |
| EF Core | 10.0 | Data persistence | Native to .NET 10, database-first scaffolding already used in Phase 1 |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| Mapperly | Latest | DTO mapping | Optional - only if complex entity-to-DTO transformations needed (commercial AutoMapper alternative) |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Blazilla | Blazored.FluentValidation v2.2.1 | Archived December 2025, maintainer recommends Blazilla |
| Blazilla | Accelist.FluentValidation.Blazor | Less flexible, fewer features, but zero-configuration approach |
| Application audit logging | Audit.EntityFramework.Core | External library adds complexity, override SaveChanges is simpler and sufficient |
| Manual mapping | Mapperly | Manual mapping is simpler for basic scenarios, use Mapperly only if complexity justifies it |

**Installation:**
```bash
dotnet add package FluentValidation
dotnet add package Blazilla
# MudBlazor already installed in Phase 3
```

## Architecture Patterns

### Recommended Project Structure
```
src/
├── SignaturPortal.Application/
│   ├── DTOs/                    # Data transfer objects for forms
│   ├── Validators/              # FluentValidation validators
│   └── Services/                # Business logic layer
├── SignaturPortal.Infrastructure/
│   ├── Data/
│   │   ├── Entities/            # EF Core entities (database-first scaffolded)
│   │   └── SignaturDbContext.cs
│   └── Repositories/
│       └── UnitOfWork.cs        # Transaction coordination
└── SignaturPortal.Web/
    └── Components/
        ├── Pages/               # Routable components
        └── Shared/              # Reusable form components
```

### Pattern 1: Command/Handler Pattern for Write Operations
**What:** Separate DTOs for create/edit operations with dedicated validators
**When to use:** All write operations (create, update, delete)
**Example:**
```csharp
// Application/DTOs/CreateActivityDto.cs
public class CreateActivityDto
{
    public string Headline { get; set; } = string.Empty;
    public string Jobtitle { get; set; } = string.Empty;
    public DateTime ApplicationDeadline { get; set; }
    public int ClientId { get; set; }
    // ... other fields
}

// Application/Validators/CreateActivityValidator.cs
public class CreateActivityValidator : AbstractValidator<CreateActivityDto>
{
    public CreateActivityValidator()
    {
        RuleFor(x => x.Headline)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.ApplicationDeadline)
            .GreaterThan(DateTime.Today)
            .WithMessage("Application deadline must be in the future");

        // Async validation example
        RuleFor(x => x.ClientId)
            .MustAsync(async (clientId, ct) => await ClientExists(clientId, ct))
            .WithMessage("Client does not exist");
    }

    private async Task<bool> ClientExists(int clientId, CancellationToken ct)
    {
        // Database validation logic
        return true;
    }
}
```

### Pattern 2: Optimistic Concurrency with RowVersion
**What:** Detect concurrent edits using EF Core's concurrency tokens
**When to use:** All update operations on entities with RowVersion/Timestamp columns
**Example:**
```csharp
// Source: https://learn.microsoft.com/en-us/ef/core/saving/concurrency
try
{
    await _unitOfWork.SaveChangesAsync(ct);
}
catch (DbUpdateConcurrencyException ex)
{
    foreach (var entry in ex.Entries)
    {
        if (entry.Entity is Eractivity activity)
        {
            var proposedValues = entry.CurrentValues;
            var databaseValues = await entry.GetDatabaseValuesAsync(ct);

            if (databaseValues == null)
            {
                // Entity was deleted by another user
                return Result.Failure("Activity was deleted by another user");
            }

            // Conflict resolution strategy (user-driven or automatic merge)
            // For user-driven: return both sets of values to UI
            // For automatic: merge and retry

            // Refresh to bypass next concurrency check
            entry.OriginalValues.SetValues(databaseValues);
        }
    }
}
```

### Pattern 3: Audit Logging via SaveChanges Override
**What:** Intercept all write operations to create audit log entries
**When to use:** All CUD operations that need audit trail
**Example:**
```csharp
// Infrastructure/Data/SignaturDbContext.Custom.cs (partial class)
public partial class SignaturDbContext
{
    public override async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        var auditEntries = BeforeSaveChanges();
        var result = await base.SaveChangesAsync(ct);
        await AfterSaveChanges(auditEntries, ct);
        return result;
    }

    private List<AuditEntry> BeforeSaveChanges()
    {
        var entries = new List<AuditEntry>();

        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            {
                var auditEntry = new AuditEntry
                {
                    EntityTypeId = GetEntityTypeId(entry.Entity),
                    EntityId = GetEntityId(entry.Entity),
                    Action = entry.State.ToString(),
                    Timestamp = DateTime.UtcNow,
                    UserId = GetCurrentUserId() // from UserSessionContext
                };
                entries.Add(auditEntry);
            }
        }

        return entries;
    }

    private async Task AfterSaveChanges(List<AuditEntry> auditEntries, CancellationToken ct)
    {
        foreach (var auditEntry in auditEntries)
        {
            UserActivityLogs.Add(new UserActivityLog
            {
                ActionUserId = auditEntry.UserId,
                EntityTypeId = auditEntry.EntityTypeId,
                EntityId = auditEntry.EntityId,
                TimeStamp = auditEntry.Timestamp,
                Log = $"{auditEntry.Action} operation on {auditEntry.EntityType}"
            });
        }

        await base.SaveChangesAsync(ct);
    }
}
```

### Pattern 4: Blazor Form with Blazilla Validation
**What:** MudBlazor form component with FluentValidation via Blazilla
**When to use:** All create/edit forms
**Example:**
```razor
@* Source: https://github.com/loresoft/Blazilla *@
@inject IValidator<CreateActivityDto> Validator

<EditForm Model="@Model" OnValidSubmit="HandleValidSubmit">
    <FluentValidator TValidator="CreateActivityValidator" />

    <MudTextField @bind-Value="Model.Headline"
                  Label="Headline"
                  Required="true"
                  For="@(() => Model.Headline)" />

    <MudDatePicker @bind-Date="Model.ApplicationDeadline"
                   Label="Application Deadline"
                   For="@(() => Model.ApplicationDeadline)" />

    <MudButton ButtonType="ButtonType.Submit"
               Variant="Variant.Filled"
               Color="Color.Primary">
        Save Activity
    </MudButton>
</EditForm>

@code {
    private CreateActivityDto Model { get; set; } = new();

    private async Task HandleValidSubmit()
    {
        // Manual async validation if needed
        var validationResult = await Validator.ValidateAsync(Model);
        if (!validationResult.IsValid)
        {
            // Handle validation errors
            return;
        }

        // Submit to service layer
        await ActivityService.CreateAsync(Model);
    }
}
```

### Pattern 5: Confirmation Dialog with MudDialog
**What:** Reusable confirmation dialog component
**When to use:** Delete operations, destructive actions
**Example:**
```csharp
// Source: https://www.c-sharpcorner.com/article/creating-a-confirmation-modal-with-blazor-mudblazor/
@inject IDialogService DialogService

@code {
    private async Task DeleteActivity(int activityId)
    {
        var parameters = new DialogParameters
        {
            ["Title"] = "Delete Activity",
            ["Message"] = "Are you sure you want to delete this recruitment activity? This action cannot be undone.",
            ["ButtonText"] = "Delete",
            ["Color"] = Color.Error
        };

        var dialog = await DialogService.ShowAsync<ConfirmationDialog>("", parameters);
        var result = await dialog.Result;

        if (!result.Canceled)
        {
            await ActivityService.DeleteAsync(activityId);
        }
    }
}

// Components/Shared/ConfirmationDialog.razor
<MudDialog>
    <DialogContent>
        <MudText>@Message</MudText>
    </DialogContent>
    <DialogActions>
        <MudButton OnClick="Cancel">Cancel</MudButton>
        <MudButton Color="@Color" Variant="Variant.Filled" OnClick="Submit">
            @ButtonText
        </MudButton>
    </DialogActions>
</MudDialog>

@code {
    [CascadingParameter] MudDialogInstance MudDialog { get; set; } = null!;
    [Parameter] public string Title { get; set; } = "Confirm";
    [Parameter] public string Message { get; set; } = string.Empty;
    [Parameter] public string ButtonText { get; set; } = "OK";
    [Parameter] public Color Color { get; set; } = Color.Primary;

    void Submit() => MudDialog.Close(DialogResult.Ok(true));
    void Cancel() => MudDialog.Cancel();
}
```

### Pattern 6: Circuit Resilience with Persistent State (.NET 10)
**What:** Use native persistent component state to survive circuit disconnections
**When to use:** Long-running forms where network disruption could lose user input
**Example:**
```razor
@* Source: https://atalupadhyay.wordpress.com/2025/06/11/building-resilient-blazor-server-apps-with-persistent-state-in-net-10/ *@
@inject PersistentComponentState PersistentState
@implements IDisposable

<EditForm Model="@Model" OnValidSubmit="HandleValidSubmit">
    @* Form fields *@
</EditForm>

@code {
    private CreateActivityDto Model { get; set; } = new();
    private PersistingComponentStateSubscription _persistingSubscription;

    protected override void OnInitialized()
    {
        // Register state persistence handler
        _persistingSubscription = PersistentState.RegisterOnPersisting(PersistFormState);

        // Restore state if available
        if (PersistentState.TryTakeFromJson<CreateActivityDto>("formState", out var restored))
        {
            Model = restored;
        }
    }

    private Task PersistFormState()
    {
        PersistentState.PersistAsJson("formState", Model);
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _persistingSubscription?.Dispose();
    }
}
```

### Anti-Patterns to Avoid
- **Validating only on client-side:** Always validate server-side; client validation is UX enhancement only
- **Using Validate() instead of ValidateAsync():** With async validators, always use ValidateAsync or exceptions occur (FluentValidation 11.x+)
- **Loading entities without .AsNoTracking() for read-only views:** Unnecessary tracking overhead in list/detail views
- **Catching DbUpdateException generically:** Distinguish between concurrency (DbUpdateConcurrencyException) and constraint violations
- **Hard-coding confirmation dialog text:** Use parameters to make dialogs reusable
- **Mixing EditForm and MudForm validation:** Choose one approach per form (EditForm for standard, MudForm for custom validation functions)
- **Not disposing PersistentComponentState subscriptions:** Memory leaks in long-running circuits

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Form validation | Custom validator classes per form | FluentValidation | Complex rule composition, async validation, reusable validators, testability |
| Optimistic concurrency | Manual version tracking | EF Core RowVersion/IsConcurrencyToken | Database-native support, automatic WHERE clause generation, DbUpdateConcurrencyException |
| Audit logging | Custom change tracking | SaveChanges override with ChangeTracker | Centralized, automatic, can't be bypassed, single point of audit logic |
| Form state persistence | LocalStorage + timers | .NET 10 PersistentComponentState | Encrypted, secure, handles circuit lifecycle, no JS interop, native API |
| Confirmation dialogs | JavaScript confirm() | MudDialog with parameters | Themeable, testable, async/await pattern, consistent with MudBlazor UI |
| Cascade delete logic | Manual child deletion loops | EF Core OnDelete() configuration | Database enforces referential integrity, prevents orphans, atomic transactions |
| DTO mapping | Manual property copying | Mapperly (if complex) | Source-generated, compile-time safety, no reflection, AOT-compatible |

**Key insight:** The ecosystem has solved CRUD complexity: validation (FluentValidation), concurrency (EF Core tokens), audit (ChangeTracker), state persistence (.NET 10 native). Building custom solutions means missing edge cases like async validator timing, concurrency conflict resolution strategies, circuit reconnection state encryption, and referential integrity cycles.

## Common Pitfalls

### Pitfall 1: Async Validation in Non-Async Context
**What goes wrong:** Using async FluentValidation rules (MustAsync) with synchronous validation triggers exceptions in FluentValidation 11.x+
**Why it happens:** Blazor's validation pipeline can be synchronous by default, but async rules require await
**How to avoid:** Always call ValidateAsync() manually before form submission if using async rules; register validators as transient/scoped if they hold state
**Warning signs:** Exceptions during validation, "async method called synchronously" errors

### Pitfall 2: Concurrency Token Not in EditForm Model
**What goes wrong:** DbUpdateConcurrencyException on every edit because RowVersion/Timestamp not included in DTO
**Why it happens:** Developer forgets to include concurrency token in edit DTO, EF Core sees original value as null
**How to avoid:** Always include RowVersion property in edit DTOs as byte[] or hidden field; scaffold DbContext inspects database schema to detect timestamp columns automatically
**Warning signs:** Every edit fails with concurrency exception, even when no concurrent users exist

### Pitfall 3: Database-First Schema Changes Break Scaffolded Entities
**What goes wrong:** Adding RowVersion column to existing table requires re-scaffolding, breaking custom partial classes
**Why it happens:** Database-first approach means entities are regenerated; custom code in generated files is lost
**How to avoid:** Use partial classes in separate files for custom entity logic; check if schema already has rowversion/timestamp columns (common in legacy schemas); document which entities have concurrency tokens
**Warning signs:** Concurrency token properties missing after scaffold, custom entity methods disappear

### Pitfall 4: Cascade Delete Cycles in Database Schema
**What goes wrong:** SQL Server error "may cause cycles or multiple cascade paths" when configuring cascade deletes
**Why it happens:** Multiple relationships form a cycle (e.g., Blog -> Post, Person -> Post, Person -> Blog creates cycle)
**How to avoid:** Use DeleteBehavior.ClientCascade for one relationship in the cycle to break it; EF Core handles cascade in memory, database stays with ON DELETE NO ACTION; ensure child entities are loaded before deleting parent if using ClientCascade
**Warning signs:** Database creation fails with cycle error, foreign key constraint exceptions during delete

### Pitfall 5: Audit Logging Infinite Loop
**What goes wrong:** SaveChanges override creates UserActivityLog entries, which triggers SaveChanges again, causing stack overflow
**Why it happens:** Adding audit entries within SaveChanges override and calling SaveChanges again
**How to avoid:** Track audit entries in BeforeSaveChanges(), call base.SaveChangesAsync() once, then call base.SaveChangesAsync() again for audit entries (not recursive override); or use a flag to prevent re-entrancy
**Warning signs:** Stack overflow exceptions, infinite loop during save, CPU spike on save operations

### Pitfall 6: MudBlazor Validation Requires `For` Parameter
**What goes wrong:** Validation messages don't display under MudTextField even though validation fails
**Why it happens:** MudBlazor components need For="@(() => Model.Property)" to wire up validation message display
**How to avoid:** Always add For parameter to MudBlazor input components when using with EditForm/FluentValidation; ErrorText property overrides validation messages, avoid setting it
**Warning signs:** Form validation works (can't submit invalid form) but no error messages visible

### Pitfall 7: Circuit Disconnection Loses Unsaved Form Data
**What goes wrong:** User fills out long form, network hiccups, circuit disconnects, all input lost
**Why it happens:** Blazor Server circuit state is in-memory; disconnection beyond reconnection window loses state
**How to avoid:** Use .NET 10 PersistentComponentState for critical forms; for .NET 9 or earlier, use Blazored.LocalStorage with EditContext.OnFieldChanged; warn users about unsaved changes with NavigationLock component
**Warning signs:** User complaints about lost data, support tickets after network issues

### Pitfall 8: Permission Check Only in UI, Not Service Layer
**What goes wrong:** User bypasses UI button visibility and calls API directly, deletes data they shouldn't
**Why it happens:** Authorization check only in @if (hasDeletePermission) around button, not in delete method
**How to avoid:** Always check permissions in service/application layer before write operations; UI checks are UX only, not security; use [Authorize] or manual permission checks in service methods
**Warning signs:** Security audit findings, unauthorized data modifications

### Pitfall 9: Not Handling Deleted Entity in Concurrency Conflict
**What goes wrong:** Edit form tries to resolve concurrency conflict, but entity was deleted by another user, throws null reference
**Why it happens:** GetDatabaseValuesAsync() returns null when entity deleted, code assumes it's always present
**How to avoid:** Check if databaseValues is null in catch block; return specific error message "Entity was deleted by another user"; redirect to list view
**Warning signs:** Null reference exceptions during concurrency conflict handling

### Pitfall 10: MudForm vs EditForm Confusion
**What goes wrong:** Using ButtonType="ButtonType.Submit" with MudForm causes double submission or validation issues
**Why it happens:** MudForm doesn't use submit buttons, EditForm does; mixing the patterns breaks validation flow
**How to avoid:** Choose one pattern per form: EditForm + Submit button + DataAnnotationsValidator/FluentValidator, OR MudForm + regular button + Validation parameter; don't mix
**Warning signs:** Form submits twice, validation doesn't fire, OnValidSubmit doesn't trigger

## Code Examples

Verified patterns from official sources:

### Handling Concurrency Conflicts with User Choice
```csharp
// Source: https://learn.microsoft.com/en-us/ef/core/saving/concurrency
public async Task<ConcurrencyResult> UpdateActivityAsync(EditActivityDto dto, CancellationToken ct)
{
    var activity = await _repository.GetByIdAsync(dto.EractivityId, ct);
    if (activity == null)
        return ConcurrencyResult.NotFound();

    // Apply changes from DTO
    activity.Headline = dto.Headline;
    activity.Jobtitle = dto.Jobtitle;
    // ... other properties

    try
    {
        await _unitOfWork.SaveChangesAsync(ct);
        return ConcurrencyResult.Success();
    }
    catch (DbUpdateConcurrencyException ex)
    {
        var entry = ex.Entries.Single();
        var proposedValues = entry.CurrentValues;
        var databaseValues = await entry.GetDatabaseValuesAsync(ct);

        if (databaseValues == null)
        {
            return ConcurrencyResult.Deleted();
        }

        // Return both sets of values for user to choose
        var currentDto = new EditActivityDto();
        var dbDto = new EditActivityDto();

        // Map proposed values
        currentDto.Headline = proposedValues.GetValue<string>(nameof(Eractivity.Headline));

        // Map database values
        dbDto.Headline = databaseValues.GetValue<string>(nameof(Eractivity.Headline));

        return ConcurrencyResult.Conflict(currentDto, dbDto);
    }
}
```

### FluentValidation with Async Database Rules
```csharp
// Source: https://docs.fluentvalidation.net/en/latest/async.html
public class EditActivityValidator : AbstractValidator<EditActivityDto>
{
    private readonly IActivityRepository _repository;

    public EditActivityValidator(IActivityRepository repository)
    {
        _repository = repository;

        RuleFor(x => x.JournalNo)
            .NotEmpty()
            .MustAsync(BeUniqueJournalNo)
            .WithMessage("Journal number must be unique");
    }

    private async Task<bool> BeUniqueJournalNo(EditActivityDto dto, string journalNo, CancellationToken ct)
    {
        // Check if journal number exists for a different activity
        var existing = await _repository.FindByJournalNoAsync(journalNo, ct);
        return existing == null || existing.EractivityId == dto.EractivityId;
    }
}
```

### Delete with Permission Check and Confirmation
```razor
@* Component: Pages/Activities/ActivityList.razor *@
@inject IDialogService DialogService
@inject IActivityService ActivityService
@inject IPermissionService PermissionService

<MudTable Items="@Activities">
    <RowTemplate>
        <MudTd>@context.Headline</MudTd>
        <MudTd>
            @if (_canDeleteActivity)
            {
                <MudIconButton Icon="@Icons.Material.Filled.Delete"
                               Color="Color.Error"
                               OnClick="@(() => DeleteActivityAsync(context))" />
            }
        </MudTd>
    </RowTemplate>
</MudTable>

@code {
    private List<ActivityDto> Activities { get; set; } = new();
    private bool _canDeleteActivity;

    protected override async Task OnInitializedAsync()
    {
        _canDeleteActivity = await PermissionService.HasPermissionAsync(
            UserName, PermissionIds.DeleteActivity);
        Activities = await ActivityService.GetAllAsync();
    }

    private async Task DeleteActivityAsync(ActivityDto activity)
    {
        var confirmed = await DialogService.ShowMessageBox(
            "Delete Activity",
            $"Are you sure you want to delete '{activity.Headline}'? This cannot be undone.",
            yesText: "Delete",
            noText: "Cancel");

        if (confirmed == true)
        {
            var result = await ActivityService.DeleteAsync(activity.EractivityId);
            if (result.IsSuccess)
            {
                Activities.Remove(activity);
                StateHasChanged();
            }
            else
            {
                // Show error snackbar
            }
        }
    }
}
```

### Cascade Delete Configuration for Database-First
```csharp
// Source: https://learn.microsoft.com/en-us/ef/core/saving/cascade-delete
// Infrastructure/Data/SignaturDbContext.Custom.cs
public partial class SignaturDbContext
{
    partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
    {
        // Override cascade behavior for specific relationships
        // NOTE: Database-first scaffolding generates relationships based on DB schema
        // Use this method to override behaviors only when needed

        // Example: Change Eractivity -> Eractivitymember to ClientCascade
        // to avoid SQL Server cascade cycle errors
        modelBuilder.Entity<Eractivitymember>()
            .HasOne(e => e.Eractivity)
            .WithMany(e => e.Eractivitymembers)
            .OnDelete(DeleteBehavior.ClientCascade); // EF handles cascade, not DB
    }
}
```

### Persistent State for Form Resilience (.NET 10)
```razor
@* Source: https://atalupadhyay.wordpress.com/2025/06/11/building-resilient-blazor-server-apps-with-persistent-state-in-net-10/ *@
@inject PersistentComponentState ApplicationState
@implements IDisposable

<EditForm Model="@Model" OnValidSubmit="HandleValidSubmit">
    <FluentValidator TValidator="CreateActivityValidator" />

    <MudTextField @bind-Value="Model.Headline" Label="Headline" For="@(() => Model.Headline)" />
    <MudTextField @bind-Value="Model.Jobtitle" Label="Job Title" For="@(() => Model.Jobtitle)" />

    <MudButton ButtonType="ButtonType.Submit">Save</MudButton>
</EditForm>

@code {
    private CreateActivityDto Model { get; set; } = new();
    private PersistingComponentStateSubscription _persistingSubscription;

    protected override void OnInitialized()
    {
        _persistingSubscription = ApplicationState.RegisterOnPersisting(PersistData);

        if (!ApplicationState.TryTakeFromJson<CreateActivityDto>("activityForm", out var restored))
        {
            Model = new CreateActivityDto();
        }
        else
        {
            Model = restored!;
        }
    }

    private Task PersistData()
    {
        ApplicationState.PersistAsJson("activityForm", Model);
        return Task.CompletedTask;
    }

    private async Task HandleValidSubmit()
    {
        await ActivityService.CreateAsync(Model);
        // Clear persisted state after successful save
        ApplicationState.PersistAsJson("activityForm", (CreateActivityDto?)null);
    }

    public void Dispose()
    {
        _persistingSubscription.Dispose();
    }
}
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Blazored.FluentValidation | Blazilla | Archived Dec 2025 | Blazored maintainer recommends Blazilla; newer library with active maintenance |
| Blazored.AutoSaveEditForm | .NET 10 PersistentComponentState | .NET 10 (2025) | Native API eliminates third-party dependency, encrypted state, built-in circuit lifecycle |
| AutoMapper | Mapperly or manual mapping | 2024-2025 | AutoMapper commercial license; Mapperly source-generated, same performance as manual |
| Manual EditContext.OnFieldChanged | NavigationLock component | ASP.NET Core 7+ | Built-in component for unsaved changes warning |
| JavaScript confirm() | MudDialog/DialogService | MudBlazor adoption | Consistent UI, themeable, async/await pattern, testable |

**Deprecated/outdated:**
- **Blazored.FluentValidation**: Archived December 26, 2025 - maintainer recommends Blazilla
- **FluentValidation 10.x and below**: Async rules ran synchronously in ASP.NET validation pipeline; 11.x throws exception, forcing correct async usage
- **Manual form state persistence**: .NET 10 PersistentComponentState eliminates need for custom LocalStorage/SessionStorage solutions

## Open Questions

1. **Does Eractivity table have RowVersion/Timestamp column?**
   - What we know: Database-first schema, legacy SQL Server database
   - What's unclear: Whether concurrency token already exists in schema
   - Recommendation: Query INFORMATION_SCHEMA.COLUMNS for rowversion/timestamp type; if not present, assess if adding it breaks legacy app

2. **Legacy app concurrent edit behavior?**
   - What we know: Legacy WebForms app shares database
   - What's unclear: Does legacy app implement optimistic concurrency? Last-write-wins?
   - Recommendation: Test concurrent edits between legacy and Blazor; if legacy doesn't have concurrency checks, adding RowVersion might cause legacy edits to fail; consider application-managed concurrency token if breaking legacy is unacceptable

3. **Audit log schema constraints?**
   - What we know: UserActivityLog table exists with ActionUserId, EntityTypeId, EntityId, Log fields
   - What's unclear: Are there foreign key constraints? Required fields? Max lengths?
   - Recommendation: Verify schema constraints; check if EntityTypeId is lookup table or free-form integer; confirm if ActionUserId must match aspnet_Users.UserId

4. **Cascade delete configuration in legacy schema?**
   - What we know: Eractivity has child tables (Eractivitymember, Ercandidate, Ercandidatefile)
   - What's unclear: Are cascade deletes configured at database level? Which DeleteBehavior is scaffolded?
   - Recommendation: Inspect scaffolded DbContext OnModelCreating() to see EF Core detected behaviors; check SQL Server foreign key constraints with sp_helpconstraint; document current behavior before making changes

5. **Blazilla vs Accelist.FluentValidation.Blazor trade-offs?**
   - What we know: Blazilla is newer, Accelist is zero-config
   - What's unclear: Performance differences? Async validation support in Accelist?
   - Recommendation: Start with Blazilla (officially recommended successor); if async validation causes issues, evaluate Accelist as fallback

## Sources

### Primary (HIGH confidence)
- [EF Core Handling Concurrency Conflicts - Microsoft Learn](https://learn.microsoft.com/en-us/ef/core/saving/concurrency) - Official documentation on optimistic concurrency, RowVersion, DbUpdateConcurrencyException handling
- [EF Core Cascade Delete - Microsoft Learn](https://learn.microsoft.com/en-us/ef/core/saving/cascade-delete) - Official documentation on DeleteBehavior, ClientCascade, cascade cycles
- [FluentValidation Official Docs - Blazor](https://docs.fluentvalidation.net/en/latest/blazor.html) - Official list of recommended third-party libraries
- [FluentValidation Async Validation](https://docs.fluentvalidation.net/en/latest/async.html) - Official guidance on ValidateAsync() requirements
- [MudBlazor Forms Documentation](https://mudblazor.com/components/form) - Official MudForm vs EditForm patterns

### Secondary (MEDIUM confidence)
- [Blazilla GitHub Repository](https://github.com/loresoft/Blazilla) - Actively maintained FluentValidation-Blazor library, successor to Blazored
- [Blazored.FluentValidation GitHub (Archived)](https://github.com/Blazored/FluentValidation) - Archived December 2025, maintainer recommends Blazilla
- [Building Resilient Blazor Server Apps with Persistent State in .NET 10](https://atalupadhyay.wordpress.com/2025/06/11/building-resilient-blazor-server-apps-with-persistent-state-in-net-10/) - .NET 10 circuit resilience features
- [Creating A Confirmation Modal With Blazor + MudBlazor](https://www.c-sharpcorner.com/article/creating-a-confirmation-modal-with-blazor-mudblazor/) - Reusable dialog pattern
- [Implementing Audit Logs in EF Core Without Polluting Your Entities](https://blog.elmah.io/implementing-audit-logs-in-ef-core-without-polluting-your-entities/) - SaveChanges override pattern
- [Mapperly: The Coolest Object Mapping Tool in Town](https://mdbouk.com/mapperly-the-coolest-object-mapping-tool-in-town/) - Source-generated mapping as AutoMapper alternative
- [Best Free Alternatives to AutoMapper in .NET — Why We Moved to Mapperly](https://abp.io/community/articles/best-free-alternatives-to-automapper-in-.net-why-we-moved-to-mapperly-l9f5ii8s) - AutoMapper commercial license, Mapperly adoption

### Tertiary (LOW confidence)
- [The performance of mappers in C#](https://www.netmentor.es/entrada/en/benchmark-mappers-csharp) - Performance benchmarks: Mapperly as fast as manual, AutoMapper 8.61x slower
- [Blazor NavigationLock for Unsaved Changes](https://www.niceonecode.com/blog/103/adding-confirmation-popup-for-any-unsaved-changes-when-navigating-away-or-dirty-state-by-using-the-naviga) - NavigationLock component usage

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH - Official documentation confirms FluentValidation, EF Core, MudBlazor patterns; Blazilla GitHub shows active maintenance and official recommendation
- Architecture: HIGH - Microsoft Learn provides detailed examples for concurrency, cascade delete, and audit logging patterns; MudBlazor docs confirm form validation approaches
- Pitfalls: HIGH - Microsoft Learn documents concurrency edge cases, cascade cycles, and DeleteBehavior options; FluentValidation docs explicitly warn about async validation timing

**Research date:** 2026-02-15
**Valid until:** 2026-03-17 (30 days - ecosystem stable, but Blazilla is new and may evolve rapidly)
