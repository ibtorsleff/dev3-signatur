using SignaturPortal.Application.DTOs;

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
    Task<GridResponse<ActivityListDto>> GetActivitiesAsync(GridRequest request, CancellationToken ct = default);
}
