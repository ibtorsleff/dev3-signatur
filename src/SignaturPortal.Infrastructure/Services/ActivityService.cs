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
    /// Gets a paginated list of candidates for a specific activity.
    /// Supports server-side filtering by name (first name or last name).
    /// Candidates are scoped by tenant through global query filters on activity.
    /// </summary>
    public async Task<GridResponse<CandidateListDto>> GetCandidatesAsync(
        int activityId,
        GridRequest request,
        CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        // Stamp tenant context for global query filters
        context.CurrentSiteId = _sessionContext.SiteId;
        context.CurrentClientId = _sessionContext.ClientId;

        // Build base query
        var query = context.Ercandidates
            .Where(c => c.EractivityId == activityId && !c.IsDeleted);

        // Apply name search filter if provided
        var nameFilter = request.Filters.FirstOrDefault(f =>
            f.PropertyName.Equals("FullName", StringComparison.OrdinalIgnoreCase) ||
            f.PropertyName.Equals("FirstName", StringComparison.OrdinalIgnoreCase) ||
            f.PropertyName.Equals("LastName", StringComparison.OrdinalIgnoreCase));

        if (nameFilter != null && nameFilter.Value is string searchTerm && !string.IsNullOrWhiteSpace(searchTerm))
        {
            query = query.Where(c =>
                c.FirstName.Contains(searchTerm) ||
                c.LastName.Contains(searchTerm));
        }

        // Get total count AFTER filters but BEFORE pagination
        var totalCount = await query.CountAsync(ct);

        // Apply sorts (default: RegistrationDate descending)
        if (request.Sorts.Count == 0)
        {
            query = query.OrderByDescending(c => c.RegistrationDate);
        }
        else
        {
            query = query.ApplySorts(request.Sorts);
        }

        // Apply pagination
        query = query.ApplyPage(request.Page, request.PageSize);

        // Project to DTO with file count subquery
        var items = await query
            .Select(c => new CandidateListDto
            {
                ErcandidateId = c.ErcandidateId,
                EractivityId = c.EractivityId,
                FirstName = c.FirstName,
                LastName = c.LastName,
                Email = c.Email,
                Telephone = c.Telephone,
                City = c.City,
                ZipCode = c.ZipCode,
                RegistrationDate = c.RegistrationDate,
                ErcandidateStatusId = c.ErcandidateStatusId,
                StatusName = "", // Will be filled in-memory
                IsDeleted = c.IsDeleted,
                FileCount = context.Ercandidatefiles.Count(f => f.ErcandidateId == c.ErcandidateId)
            })
            .ToListAsync(ct);

        // Compute StatusName in-memory using StatusMappings
        foreach (var item in items)
        {
            var statusName = StatusMappings.GetCandidateStatusName(item.ErcandidateStatusId);
            var index = items.IndexOf(item);
            items[index] = item with { StatusName = statusName };
        }

        return new GridResponse<CandidateListDto>
        {
            Items = items,
            TotalCount = totalCount
        };
    }

    /// <summary>
    /// Gets detailed information for a specific candidate including file attachments.
    /// Returns null if candidate not found or belongs to a different tenant.
    /// </summary>
    public async Task<CandidateDetailDto?> GetCandidateDetailAsync(
        int activityId,
        int candidateId,
        CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        // Stamp tenant context for global query filters
        context.CurrentSiteId = _sessionContext.SiteId;
        context.CurrentClientId = _sessionContext.ClientId;

        // Query candidate with security check (both activityId and candidateId must match)
        var candidate = await context.Ercandidates
            .Where(c => c.ErcandidateId == candidateId && c.EractivityId == activityId)
            .Select(c => new
            {
                Candidate = c,
                ActivityHeadline = c.Eractivity.Headline,
                Files = context.Ercandidatefiles
                    .Where(f => f.ErcandidateId == c.ErcandidateId)
                    .Select(f => new CandidateFileDto
                    {
                        BinaryFileId = f.BinaryFileId,
                        FileName = f.BinaryFile.FileName,
                        FileSize = f.BinaryFile.FileSize
                    })
                    .ToList()
            })
            .FirstOrDefaultAsync(ct);

        if (candidate == null)
            return null;

        var c = candidate.Candidate;
        var statusName = StatusMappings.GetCandidateStatusName(c.ErcandidateStatusId);

        return new CandidateDetailDto
        {
            ErcandidateId = c.ErcandidateId,
            EractivityId = c.EractivityId,
            FirstName = c.FirstName,
            LastName = c.LastName,
            Email = c.Email,
            Telephone = c.Telephone,
            Address = c.Address,
            City = c.City,
            ZipCode = c.ZipCode,
            RegistrationDate = c.RegistrationDate,
            DateOfBirth = c.DateOfBirth,
            ErcandidateStatusId = c.ErcandidateStatusId,
            StatusName = statusName,
            IsDeleted = c.IsDeleted,
            IsInternalCandidate = c.IsInternalCandidate,
            LanguageId = c.LanguageId,
            Files = candidate.Files,
            ActivityHeadline = candidate.ActivityHeadline
        };
    }

    /// <summary>
    /// Gets binary file data for a candidate attachment.
    /// Returns null if file not found or user doesn't have access.
    /// Verifies file ownership through candidate-activity-tenant chain.
    /// </summary>
    public async Task<(byte[] FileData, string FileName)?> GetCandidateFileDataAsync(
        int candidateId,
        int binaryFileId,
        CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        // Stamp tenant context for global query filters
        context.CurrentSiteId = _sessionContext.SiteId;
        context.CurrentClientId = _sessionContext.ClientId;

        // Verify file ownership through candidate-activity-tenant chain
        // Global filter on Eractivity ensures tenant isolation
        var fileOwnership = await context.Ercandidatefiles
            .Where(f => f.ErcandidateId == candidateId && f.BinaryFileId == binaryFileId)
            .Select(f => new
            {
                Exists = true,
                ActivityClientId = f.Ercandidate.Eractivity.ClientId
            })
            .FirstOrDefaultAsync(ct);

        // If file doesn't exist or belongs to different tenant, return null
        if (fileOwnership == null)
            return null;

        // Load the actual file data (only for this specific file)
        var file = await context.Set<Infrastructure.Data.Entities.BinaryFile>()
            .Where(bf => bf.BinaryFileId == binaryFileId)
            .Select(bf => new { bf.FileData, bf.FileName })
            .FirstOrDefaultAsync(ct);

        if (file?.FileData == null)
            return null;

        return (file.FileData, file.FileName);
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
