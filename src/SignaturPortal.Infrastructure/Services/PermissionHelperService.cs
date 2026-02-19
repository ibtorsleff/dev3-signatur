using SignaturPortal.Application.Authorization;
using SignaturPortal.Application.Interfaces;

namespace SignaturPortal.Infrastructure.Services;

/// <summary>
/// Composite permission helper mirroring legacy PermissionHelper.
/// Registered as Scoped (uses scoped IUserSessionContext and IPermissionService).
/// IPermissionService already caches permissions per-request, so multiple HasPermissionAsync
/// calls within the same scope are efficient (single DB query, then set lookups).
/// </summary>
public class PermissionHelperService : IPermissionHelper
{
    private readonly IPermissionService _permissionService;
    private readonly IUserSessionContext _session;

    public PermissionHelperService(IPermissionService permissionService, IUserSessionContext session)
    {
        _permissionService = permissionService;
        _session = session;
    }

    private async Task<bool> HasPermissionAsync(PortalPermission permission, CancellationToken ct)
    {
        if (!_session.IsInitialized || string.IsNullOrEmpty(_session.UserName))
            return false;
        return await _permissionService.HasPermissionAsync(_session.UserName, (int)permission, ct);
    }

    // --- Ad Portal ---

    public async Task<bool> UserCanAccessAdPortalAsync(CancellationToken ct = default)
        => await HasPermissionAsync(PortalPermission.AdPortalAccess, ct);

    public async Task<bool> UserHasAdminAccessAsync(CancellationToken ct = default)
        => await HasPermissionAsync(PortalPermission.AdPortalAccess, ct)
           && await HasPermissionAsync(PortalPermission.AdPortalAdminAccess, ct);

    // --- Recruitment Portal ---

    public async Task<bool> UserCanAccessRecruitmentAsync(CancellationToken ct = default)
        => await HasPermissionAsync(PortalPermission.RecruitmentPortalRecruitmentAccess, ct);

    public async Task<bool> UserCanAccessRecruitmentAdminAsync(CancellationToken ct = default)
        => await HasPermissionAsync(PortalPermission.RecruitmentPortalRecruitmentAccess, ct)
           && await HasPermissionAsync(PortalPermission.RecruitmentPortalAdminAccess, ct);

    public async Task<bool> UserCanAccessRecruitmentDraftActivitiesAsync(CancellationToken ct = default)
        => await HasPermissionAsync(PortalPermission.RecruitmentPortalViewDraftActivities, ct)
           || await HasPermissionAsync(PortalPermission.RecruitmentPortalEditDraftActivities, ct);

    public async Task<bool> UserCanAccessCandidateDetailsAsync(CancellationToken ct = default)
        => await HasPermissionAsync(PortalPermission.RecruitmentPortalRecruitmentAccess, ct)
           && (await HasPermissionAsync(PortalPermission.RecruitmentPortalViewCandidateDetails, ct)
               || await HasPermissionAsync(PortalPermission.RecruitmentPortalCandidateEvaluation, ct)
               || await HasPermissionAsync(PortalPermission.RecruitmentPortalEditCandidate, ct)
               || await HasPermissionAsync(PortalPermission.RecruitmentPortalViewCandidateNotes, ct)
               || await HasPermissionAsync(PortalPermission.RecruitmentPortalCreateCandidateNote, ct)
               || await HasPermissionAsync(PortalPermission.RecruitmentPortalCandidateNoteCanDeleteOtherUsersNote, ct));

    public async Task<bool> UserCanAccessCandidateNotesAsync(CancellationToken ct = default)
        => await HasPermissionAsync(PortalPermission.RecruitmentPortalRecruitmentAccess, ct)
           && (await HasPermissionAsync(PortalPermission.RecruitmentPortalEditCandidate, ct)
               || await HasPermissionAsync(PortalPermission.RecruitmentPortalViewCandidateNotes, ct)
               || await HasPermissionAsync(PortalPermission.RecruitmentPortalCreateCandidateNote, ct)
               || await HasPermissionAsync(PortalPermission.RecruitmentPortalCandidateNoteCanDeleteOtherUsersNote, ct));

    public async Task<bool> UserCanAccessActivitiesUserNotMemberOfAsync(CancellationToken ct = default)
        => await HasPermissionAsync(PortalPermission.RecruitmentPortalRecruitmentAccess, ct)
           && (await HasPermissionAsync(PortalPermission.RecruitmentPortalViewActivitiesUserNotMemberOf, ct)
               || await HasPermissionAsync(PortalPermission.RecruitmentPortalEditActivitiesUserNotMemberOf, ct));

    public async Task<bool> UserCanAccessActivitiesWithWorkAreaUserNotMemberOfAsync(CancellationToken ct = default)
        => await HasPermissionAsync(PortalPermission.RecruitmentPortalRecruitmentAccess, ct)
           && (await HasPermissionAsync(PortalPermission.RecruitmentPortalViewActivitiesFromWorkAreaUserNotMemberOf, ct)
               || await HasPermissionAsync(PortalPermission.RecruitmentPortalEditActivitiesFromWorkAreaUserNotMemberOf, ct));

    public async Task<bool> UserCanCreateActivityAsync(CancellationToken ct = default)
        => await HasPermissionAsync(PortalPermission.RecruitmentPortalRecruitmentAccess, ct)
           && await HasPermissionAsync(PortalPermission.RecruitmentPortalCreateActivity, ct);

    public async Task<bool> UserCanExportActivityMembersAsync(CancellationToken ct = default)
        => await HasPermissionAsync(PortalPermission.RecruitmentPortalAllowExportActivityMembersInActivityList, ct);
}
