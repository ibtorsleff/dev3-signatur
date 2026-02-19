using SignaturPortal.Application.DTOs;
using SignaturPortal.Domain.Enums;

namespace SignaturPortal.Application.Interfaces;

/// <summary>
/// Service for activity-related operations.
/// Provides server-side paginated, sorted, and filtered activity queries.
/// </summary>
public interface IActivityService
{
    /// <summary>
    /// Gets a paginated list of activities with server-side sorting and filtering.
    /// Results are automatically scoped to the current user's tenant (ClientId).
    /// Non-admin users see only activities where they are Responsible or CreatedBy.
    /// </summary>
    Task<GridResponse<ActivityListDto>> GetActivitiesAsync(
        GridRequest request,
        ERActivityStatus? statusFilter = null,
        int? clientIdFilter = null,
        ActivityListFilterDto? moreFilters = null,
        CancellationToken ct = default);

    /// <summary>
    /// Gets dropdown options for the "More" filter panel in the activity list.
    /// Returns distinct CreatedBy users, Recruitment Responsible users, and Client Sections
    /// derived from existing activities for the given status and client context.
    /// Only called for non-client users.
    /// </summary>
    Task<ActivityFilterOptionsDto> GetActivityFilterOptionsAsync(
        ERActivityStatus status,
        int? clientIdFilter = null,
        CancellationToken ct = default);

    /// <summary>
    /// Gets detailed information for a specific activity including hiring team members and candidate count.
    /// Returns null if activity not found or user doesn't have access (tenant filtering).
    /// </summary>
    Task<ActivityDetailDto?> GetActivityDetailAsync(int activityId, CancellationToken ct = default);

    /// <summary>
    /// Gets a paginated list of candidates for a specific activity.
    /// Supports server-side filtering by name (search string).
    /// Results are scoped to the user's tenant via global query filters.
    /// </summary>
    Task<GridResponse<CandidateListDto>> GetCandidatesAsync(int activityId, GridRequest request, CancellationToken ct = default);

    /// <summary>
    /// Gets detailed information for a specific candidate including file attachments.
    /// Returns null if candidate not found or belongs to a different tenant.
    /// Verifies both activityId and candidateId match for security.
    /// </summary>
    Task<CandidateDetailDto?> GetCandidateDetailAsync(int activityId, int candidateId, CancellationToken ct = default);

    /// <summary>
    /// Gets binary file data for a candidate attachment.
    /// Returns null if file not found or user doesn't have access.
    /// Verifies file ownership through candidate-activity-tenant chain.
    /// </summary>
    Task<(byte[] FileData, string FileName)?> GetCandidateFileDataAsync(int candidateId, int binaryFileId, CancellationToken ct = default);

    /// <summary>
    /// Gets all activity members for Excel export.
    /// Applies the same tenant + permission filters as GetActivitiesAsync (no pagination).
    /// Returns one row per member per activity; activities with no members are excluded.
    /// </summary>
    Task<List<ActivityMemberExportRow>> GetActivityMembersForExportAsync(
        int clientId,
        ERActivityStatus status,
        ActivityListFilterDto? moreFilters = null,
        CancellationToken ct = default);

    /// <summary>
    /// Counts active (OnGoing) activities the given user is associated with
    /// as a member, creator, or responsible person.
    /// Used for the external-user access guard: if count is zero the user sees
    /// a disclaimer and is logged out (matches legacy UserInActiveActivitiesCount).
    /// </summary>
    Task<int> GetUserActiveActivitiesCountAsync(Guid userId, CancellationToken ct = default);
}
