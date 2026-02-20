namespace SignaturPortal.Application.Interfaces;

/// <summary>
/// Composite permission checks mirroring legacy PermissionHelper static methods.
/// All methods are async because they query permissions via IPermissionService (EF Core).
/// Load results in OnInitializedAsync, store in component bool fields, use in Razor markup.
/// </summary>
public interface IPermissionHelper
{
    // Ad Portal
    Task<bool> UserCanAccessAdPortalAsync(CancellationToken ct = default);
    Task<bool> UserHasAdminAccessAsync(CancellationToken ct = default);

    // Recruitment Portal
    Task<bool> UserCanAccessRecruitmentAsync(CancellationToken ct = default);
    Task<bool> UserCanAccessRecruitmentAdminAsync(CancellationToken ct = default);
    Task<bool> UserCanAccessRecruitmentDraftActivitiesAsync(CancellationToken ct = default);
    Task<bool> UserCanAccessCandidateDetailsAsync(CancellationToken ct = default);
    Task<bool> UserCanAccessCandidateNotesAsync(CancellationToken ct = default);
    Task<bool> UserCanAccessActivitiesUserNotMemberOfAsync(CancellationToken ct = default);
    Task<bool> UserCanAccessActivitiesWithWorkAreaUserNotMemberOfAsync(CancellationToken ct = default);
    Task<bool> UserCanCreateActivityAsync(CancellationToken ct = default);
    Task<bool> UserCanCreateDraftActivityAsync(CancellationToken ct = default);
    Task<bool> UserCanExportActivityMembersAsync(CancellationToken ct = default);
    Task<bool> UserCanEditActivitiesNotMemberOfAsync(CancellationToken ct = default);
    Task<bool> UserCanPublishWebAdAsync(CancellationToken ct = default);
}
