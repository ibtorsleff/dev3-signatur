namespace SignaturPortal.Application.Authorization;

/// <summary>
/// Subset of legacy PermissionHelper.Permission enum relevant to E-recruitment.
/// Values must match the PermissionId column in the Permission table.
/// </summary>
public enum ERecruitmentPermission
{
    RecruitmentAccess = 2000,
    ViewCandidateDetails = 2050,
    EditCandidate = 2060,
    AdminAccess = 2500,
    ViewUsers = 2550,
    CreateUsers = 2551,
}
