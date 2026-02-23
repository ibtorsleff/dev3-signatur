using SignaturPortal.Application.DTOs;
using SignaturPortal.Domain.Enums;

namespace SignaturPortal.Application.Interfaces;

/// <summary>
/// Service for activity-related operations.
/// Provides server-side paginated, sorted, and filtered activity queries.
/// </summary>
public interface IErActivityService
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
        int draftAreaTypeId = 0,
        bool includeEmailWarning = false,
        bool includeWebAdStatus = false,
        bool includeWebAdChanges = false,
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

    /// <summary>
    /// Writes a UserActivityLog entry recording that an external user was force-logged out
    /// because they have no active recruitment activities.
    /// Matches legacy: HelperERecruiting.UserActivityLogCreate(siteId, userId, "Tvunget logget ud...", null).
    /// </summary>
    Task LogExternalUserForceLogoutAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Writes a UserActivityLog entry recording that an external user was force-logged out
    /// because their client does not have the recruitment portal enabled.
    /// Matches legacy ActivityList.aspx.cs:286.
    /// </summary>
    Task LogClientNoRecruitmentPortalForceLogoutAsync(Guid userId, CancellationToken ct = default);

    // -------------------------------------------------------------------------
    // Form load / save
    // -------------------------------------------------------------------------

    /// <summary>
    /// Loads the full activity data for editing, including all field values needed
    /// to pre-populate the ActivityCreateEdit form.
    /// Returns null if not found or access denied.
    /// </summary>
    Task<ActivityEditDto?> GetActivityForEditAsync(int activityId, CancellationToken ct = default);

    /// <summary>
    /// Loads all dropdown option lists required for the ActivityCreateEdit form
    /// for the given client. Includes statuses, occupations, sections, templates, etc.
    /// </summary>
    Task<ActivityFormOptionsDto> GetActivityFormOptionsAsync(
        int clientId,
        int? currentTemplateGroupId = null,
        CancellationToken ct = default);

    /// <summary>
    /// Live-searches client sections for the MudAutocomplete on the create/edit form.
    /// Returns sections matching the search term for the given client.
    /// </summary>
    Task<List<ClientSectionDropdownDto>> GetClientSectionsForFormAsync(
        int clientId,
        string? search,
        CancellationToken ct = default);

    /// <summary>
    /// Live-searches users for the UserPickerDialog.
    /// Returns users for the given client matching the search term.
    /// </summary>
    Task<List<UserDropdownDto>> GetUsersForPickerAsync(
        int clientId,
        string? search,
        CancellationToken ct = default);

    /// <summary>
    /// Creates a new activity from the given command.
    /// Returns the new activity's EractivityId.
    /// </summary>
    Task<int> CreateActivityAsync(ActivitySaveCommand command, CancellationToken ct = default);

    /// <summary>
    /// Updates an existing activity with the given command.
    /// </summary>
    Task UpdateActivityAsync(int activityId, ActivitySaveCommand command, CancellationToken ct = default);

    /// <summary>
    /// Soft-deletes an activity (sets status to Deleted).
    /// </summary>
    Task DeleteActivityAsync(int activityId, CancellationToken ct = default);

    /// <summary>
    /// Closes an activity (sets status to Closed).
    /// </summary>
    Task CloseActivityAsync(int activityId, CancellationToken ct = default);
}
