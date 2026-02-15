using Microsoft.EntityFrameworkCore;
using SignaturPortal.Application.Authorization;
using SignaturPortal.Application.DTOs;
using SignaturPortal.Application.Interfaces;
using SignaturPortal.Domain.Helpers;
using SignaturPortal.Infrastructure.Data;
using SignaturPortal.Infrastructure.Extensions;

namespace SignaturPortal.Infrastructure.Services;

/// <summary>
/// Service for activity-related operations.
/// Implements server-side pagination, sorting, filtering with tenant isolation.
/// </summary>
public class ActivityService : IActivityService
{
    private readonly IDbContextFactory<SignaturDbContext> _contextFactory;
    private readonly IUserSessionContext _sessionContext;
    private readonly IPermissionService _permissionService;

    public ActivityService(
        IDbContextFactory<SignaturDbContext> contextFactory,
        IUserSessionContext sessionContext,
        IPermissionService permissionService)
    {
        _contextFactory = contextFactory;
        _sessionContext = sessionContext;
        _permissionService = permissionService;
    }

    /// <summary>
    /// Gets a paginated list of activities with server-side sorting and filtering.
    /// Activities are filtered by tenant (ClientId via global query filter).
    /// Non-admin users see only activities where they are Responsible or CreatedBy.
    /// </summary>
    public async Task<GridResponse<ActivityListDto>> GetActivitiesAsync(
        GridRequest request,
        CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        // Stamp tenant context for global query filters
        context.CurrentSiteId = _sessionContext.SiteId;
        context.CurrentClientId = _sessionContext.ClientId;

        // Get current user's UserGuid for permission filtering
        var currentUserGuid = await GetUserGuidAsync(context, _sessionContext.UserName, ct);

        // Check if user has admin access (can see all activities)
        var hasAdminAccess = await _permissionService.HasPermissionAsync(
            _sessionContext.UserName,
            (int)ERecruitmentPermission.AdminAccess,
            ct);

        // Build base query
        var query = context.Eractivities
            .Where(a => !a.IsCleaned); // Exclude cleaned activities

        // Apply permission-based filtering for non-admin users
        if (!hasAdminAccess && currentUserGuid.HasValue)
        {
            query = query.Where(a =>
                a.Responsible == currentUserGuid.Value ||
                a.CreatedBy == currentUserGuid.Value);
        }

        // Apply filters from request
        query = query.ApplyFilters(request.Filters);

        // Get total count AFTER filters but BEFORE pagination
        var totalCount = await query.CountAsync(ct);

        // Apply sorts (default: ApplicationDeadline descending)
        if (request.Sorts.Count == 0)
        {
            query = query.OrderByDescending(a => a.ApplicationDeadline);
        }
        else
        {
            query = query.ApplySorts(request.Sorts);
        }

        // Apply pagination
        query = query.ApplyPage(request.Page, request.PageSize);

        // Project to DTO with candidate count subquery
        // NOTE: StatusName cannot be computed in SQL, so we project EractivityStatusId
        // and compute StatusName after materialization
        var items = await query
            .Select(a => new ActivityListDto
            {
                EractivityId = a.EractivityId,
                Headline = a.Headline,
                Jobtitle = a.Jobtitle,
                JournalNo = a.JournalNo,
                ApplicationDeadline = a.ApplicationDeadline,
                EractivityStatusId = a.EractivityStatusId,
                StatusName = "", // Will be filled in-memory
                CreateDate = a.CreateDate,
                CandidateCount = a.Ercandidates.Count(c => !c.IsDeleted),
                Responsible = a.Responsible,
                CreatedBy = a.CreatedBy
            })
            .ToListAsync(ct);

        // Compute StatusName in-memory using StatusMappings
        foreach (var item in items)
        {
            // Use with statement to assign to init-only property
            var statusName = StatusMappings.GetActivityStatusName(item.EractivityStatusId);
            // Create new instance with updated StatusName
            var index = items.IndexOf(item);
            items[index] = item with { StatusName = statusName };
        }

        return new GridResponse<ActivityListDto>
        {
            Items = items,
            TotalCount = totalCount
        };
    }

    /// <summary>
    /// Gets the UserId (GUID) for a given username.
    /// Returns null if user not found.
    /// </summary>
    private async Task<Guid?> GetUserGuidAsync(
        SignaturDbContext context,
        string userName,
        CancellationToken ct)
    {
        var user = await context.AspnetUsers
            .Where(u => u.UserName == userName)
            .Select(u => u.UserId)
            .FirstOrDefaultAsync(ct);

        return user == default ? null : user;
    }
}
