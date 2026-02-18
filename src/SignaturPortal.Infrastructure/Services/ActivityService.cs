using Microsoft.EntityFrameworkCore;
using SignaturPortal.Application.Authorization;
using SignaturPortal.Application.DTOs;
using SignaturPortal.Application.Interfaces;
using SignaturPortal.Domain.Helpers;
using SignaturPortal.Infrastructure.Data;
using SignaturPortal.Domain.Enums;
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
    private readonly ICurrentUserService _currentUserService;

    public ActivityService(
        IDbContextFactory<SignaturDbContext> contextFactory,
        IUserSessionContext sessionContext,
        IPermissionService permissionService,
        ICurrentUserService currentUserService)
    {
        _contextFactory = contextFactory;
        _sessionContext = sessionContext;
        _permissionService = permissionService;
        _currentUserService = currentUserService;
    }

    /// <summary>
    /// Gets a paginated list of activities with server-side sorting and filtering.
    /// Activities are filtered by tenant (ClientId via global query filter).
    /// Non-admin users see only activities where they are Responsible or CreatedBy.
    /// </summary>
    public async Task<GridResponse<ActivityListDto>> GetActivitiesAsync(
        GridRequest request,
        ERActivityStatus? statusFilter = null,
        int? clientIdFilter = null,
        ActivityListFilterDto? moreFilters = null,
        CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        // Stamp tenant context for global query filters
        context.CurrentSiteId = _sessionContext.SiteId;

        // Use explicit client filter if provided (non-client user selected a client),
        // otherwise fall back to session ClientId (null for non-client = show all)
        if (clientIdFilter.HasValue && clientIdFilter.Value > 0)
            context.CurrentClientId = clientIdFilter.Value;
        else
            context.CurrentClientId = _sessionContext.ClientId;

        System.Diagnostics.Debug.WriteLine($"[DEBUG] Session: Init={_sessionContext.IsInitialized}, User='{_sessionContext.UserName}', SiteId={_sessionContext.SiteId}, ClientId={_sessionContext.ClientId}, ClientIdFilter={clientIdFilter}");

        // Load the current user by UserName (auth identity name) — Guid comes from the [User] DB record
        var currentUser = await _currentUserService.GetCurrentUserAsync(ct);
        var currentUserGuid = currentUser?.UserId;

        // Check if user has admin access (can see all activities)
        var hasAdminAccess = await _permissionService.HasPermissionAsync(
            _sessionContext.UserName,
            (int)PortalPermission.RecruitmentPortalAdminAccess,
            ct);

        System.Diagnostics.Debug.WriteLine($"[DEBUG] UserGuid={currentUserGuid}, Admin={hasAdminAccess}");

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

        // Filter by activity status mode (Ongoing, Closed, Draft)
        if (statusFilter.HasValue)
        {
            var statusId = (int)statusFilter.Value;
            query = query.Where(a => a.EractivityStatusId == statusId);
        }

        // Apply filters from request
        query = query.ApplyFilters(request.Filters);

        // Apply "More" toolbar panel filters (non-client users only)
        if (moreFilters != null)
        {
            if (moreFilters.CreatedByUserId.HasValue)
                query = query.Where(a => a.CreatedBy == moreFilters.CreatedByUserId.Value);

            if (moreFilters.RecruitmentResponsibleUserId.HasValue)
                query = query.Where(a => a.Responsible == moreFilters.RecruitmentResponsibleUserId.Value);

            if (moreFilters.ClientSectionId.HasValue)
                query = query.Where(a => a.ClientSectionId == moreFilters.ClientSectionId.Value);

            if (moreFilters.TemplateGroupId.HasValue)
                query = query.Where(a => a.ErtemplateGroupId == moreFilters.TemplateGroupId.Value);

            if (moreFilters.DateFrom.HasValue)
                query = query.Where(a => a.CreateDate >= moreFilters.DateFrom.Value.Date);

            if (moreFilters.DateTo.HasValue)
                query = query.Where(a => a.CreateDate < moreFilters.DateTo.Value.Date.AddDays(1));
        }

        // Get total count AFTER filters but BEFORE pagination
        var totalCount = await query.CountAsync(ct);
        System.Diagnostics.Debug.WriteLine($"[DEBUG] TotalCount={totalCount}");

        // Apply sorts (default: CreateDate descending — newest first)
        // Map DTO property names to entity property paths for server-side sorting
        var sortPropertyMap = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["ClientSectionName"] = "ClientSection.Name",
            ["TemplateGroupName"] = "ErTemplateGroup.Name",
            // These are resolved via correlated subqueries after sort+pagination — no sortable entity path.
            // Sorting is disabled on these columns in the UI (Sortable="false").
            // To make them sortable: rewrite query to JOIN aspnet_Users/WebAdVisitor upfront, project
            // all sortable columns into an intermediate queryable, then apply Dynamic LINQ sort + pagination
            // on the joined result. This keeps sorting in SQL but requires a more complex query.
            ["RecruitingResponsibleName"] = null,
            ["CreatedByName"] = null,
            ["DraftResponsibleName"] = null,
            ["WebAdVisitors"] = null
        };

        if (request.Sorts.Count == 0)
        {
            query = query.OrderByDescending(a => a.CreateDate);
        }
        else
        {
            var mappedSorts = request.Sorts
                .Select(s =>
                {
                    if (sortPropertyMap.TryGetValue(s.PropertyName, out var mapped))
                        return mapped != null ? new Application.DTOs.SortDefinition(mapped, s.Descending) : null;
                    return s;
                })
                .Where(s => s != null)
                .ToList()!;

            query = mappedSorts.Count > 0
                ? query.ApplySorts(mappedSorts!)
                : query.OrderByDescending(a => a.CreateDate);
        }

        // Apply pagination
        query = query.ApplyPage(request.Page, request.PageSize);

        // Project to DTO with candidate count subquery and resolved display names
        // User names resolved via correlated subqueries (translate to SQL scalar subselects)
        // ClientSection/TemplateGroup names resolved via navigation properties (LEFT JOIN)
        var items = await query
            .Select(a => new ActivityListDto
            {
                EractivityId = a.EractivityId,
                Headline = a.Headline,
                Jobtitle = a.Jobtitle,
                ApplicationDeadline = a.ApplicationDeadline,
                ContinuousPosting = a.ContinuousPosting,
                EractivityStatusId = a.EractivityStatusId,
                CreateDate = a.CreateDate,
                CandidateCount = a.Ercandidates.Count(c => !c.IsDeleted),
                // Web ad visitor count via correlated subquery (LEFT JOIN on WebAdId)
                WebAdVisitors = a.WebAdId.HasValue
                    ? context.WebAdVisitors.Where(w => w.WebAdId == a.WebAdId.Value).Select(w => w.Visitors).FirstOrDefault()
                    : 0,
                // Resolve user names via correlated subqueries (equivalent to LEFT JOIN)
                RecruitingResponsibleName = a.Responsible.HasValue
                    ? context.Users.Where(u => u.UserId == a.Responsible.Value).Select(u => u.FullName ?? u.UserName ?? "").FirstOrDefault() ?? ""
                    : "",
                CreatedByName = a.CreatedBy.HasValue
                    ? context.Users.Where(u => u.UserId == a.CreatedBy.Value).Select(u => u.FullName ?? u.UserName ?? "").FirstOrDefault() ?? ""
                    : "",
                DraftResponsibleName = a.DraftResponsible.HasValue
                    ? context.Users.Where(u => u.UserId == a.DraftResponsible.Value).Select(u => u.FullName ?? u.UserName ?? "").FirstOrDefault() ?? ""
                    : "",
                // Resolve lookup names via navigation properties (LEFT JOIN)
                ClientSectionName = a.ClientSection != null ? a.ClientSection.Name : "",
                TemplateGroupName = a.ErTemplateGroup != null ? a.ErTemplateGroup.Name : ""
            })
            .ToListAsync(ct);

        return new GridResponse<ActivityListDto>
        {
            Items = items,
            TotalCount = totalCount
        };
    }

    /// <summary>
    /// Gets detailed information for a specific activity including hiring team members and candidate count.
    /// Returns null if activity not found or user doesn't have access (tenant filtering).
    /// </summary>
    public async Task<ActivityDetailDto?> GetActivityDetailAsync(
        int activityId,
        CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        // Stamp tenant context for global query filters
        context.CurrentSiteId = _sessionContext.SiteId;
        context.CurrentClientId = _sessionContext.ClientId;

        // Query activity with all details
        // Use AsSplitQuery to avoid cartesian explosion between members and candidates
        var activity = await context.Eractivities
            .Where(a => a.EractivityId == activityId && !a.IsCleaned)
            .Select(a => new
            {
                a.EractivityId,
                a.ClientId,
                a.Headline,
                a.Jobtitle,
                a.JournalNo,
                a.EractivityStatusId,
                a.ApplicationDeadline,
                a.HireDate,
                a.HireDateFreeText,
                a.CreateDate,
                a.StatusChangedTimeStamp,
                a.ContinuousPosting,
                a.CandidateEvaluationEnabled,
                a.IsCleaned,
                a.EmailOnNewCandidate,
                a.Responsible,
                a.CreatedBy,
                CandidateCount = a.Ercandidates.Count(c => !c.IsDeleted),
                HiringTeamMembers = a.Eractivitymembers
                    .Select(m => new
                    {
                        m.EractivityMemberId,
                        m.UserId,
                        m.EractivityMemberTypeId,
                        m.ExtUserAllowCandidateManagement,
                        m.ExtUserAllowCandidateReview,
                        m.ExtUserAllowViewEditNotes,
                        m.NotificationMailSendToUser,
                        // Join to User table for name/email
                        m.User.UserName,
                        m.User.FullName,
                        m.User.Email
                    })
                    .ToList()
            })
            .AsSplitQuery()
            .FirstOrDefaultAsync(ct);

        if (activity == null)
        {
            return null;
        }

        // Get responsible and created by user names
        var userIds = new[] { activity.Responsible, activity.CreatedBy }
            .Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();
        var userLookup = await context.Users
            .Where(u => userIds.Contains(u.UserId))
            .ToDictionaryAsync(u => u.UserId, u => u.FullName ?? u.UserName ?? "", ct);

        var responsibleName = activity.Responsible.HasValue
            ? userLookup.GetValueOrDefault(activity.Responsible.Value, "Unknown") : "Unknown";
        var createdByName = activity.CreatedBy.HasValue
            ? userLookup.GetValueOrDefault(activity.CreatedBy.Value, "Unknown") : "Unknown";

        // Map hiring team members to DTOs
        // MemberTypeName must be computed in-memory (StatusMappings cannot be translated to SQL)
        var hiringTeamDtos = activity.HiringTeamMembers
            .Select(m => new HiringTeamMemberDto
            {
                EractivityMemberId = m.EractivityMemberId,
                UserId = m.UserId,
                UserName = m.UserName ?? "",
                FullName = m.FullName ?? "",
                Email = m.Email ?? "",
                MemberTypeId = m.EractivityMemberTypeId,
                MemberTypeName = StatusMappings.GetActivityMemberTypeName(m.EractivityMemberTypeId),
                AllowCandidateManagement = m.ExtUserAllowCandidateManagement,
                AllowCandidateReview = m.ExtUserAllowCandidateReview,
                AllowViewEditNotes = m.ExtUserAllowViewEditNotes,
                NotificationMailSendToUser = m.NotificationMailSendToUser
            })
            .ToList();

        // Create result DTO
        // StatusName must be computed in-memory (StatusMappings cannot be translated to SQL)
        var result = new ActivityDetailDto
        {
            EractivityId = activity.EractivityId,
            ClientId = activity.ClientId,
            Headline = activity.Headline,
            Jobtitle = activity.Jobtitle,
            JournalNo = activity.JournalNo,
            EractivityStatusId = activity.EractivityStatusId,
            StatusName = StatusMappings.GetActivityStatusName(activity.EractivityStatusId),
            ApplicationDeadline = activity.ApplicationDeadline,
            HireDate = activity.HireDate,
            HireDateFreeText = activity.HireDateFreeText,
            CreateDate = activity.CreateDate,
            StatusChangedTimeStamp = activity.StatusChangedTimeStamp,
            ContinuousPosting = activity.ContinuousPosting,
            CandidateEvaluationEnabled = activity.CandidateEvaluationEnabled,
            IsCleaned = activity.IsCleaned,
            EmailOnNewCandidate = activity.EmailOnNewCandidate,
            Responsible = activity.Responsible,
            ResponsibleName = responsibleName,
            CreatedBy = activity.CreatedBy,
            CreatedByName = createdByName,
            CandidateCount = activity.CandidateCount,
            HiringTeamMemberCount = hiringTeamDtos.Count,
            HiringTeamMembers = hiringTeamDtos
        };

        return result;
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
        for (var i = 0; i < items.Count; i++)
        {
            items[i] = items[i] with { StatusName = StatusMappings.GetCandidateStatusName(items[i].ErcandidateStatusId) };
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
    /// Gets dropdown options for the "More" filter panel in the activity list.
    /// Returns distinct CreatedBy users, Recruitment Responsible users, and Client Sections
    /// derived from activities matching the given status and client context.
    /// </summary>
    public async Task<ActivityFilterOptionsDto> GetActivityFilterOptionsAsync(
        ERActivityStatus status,
        int? clientIdFilter = null,
        CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        context.CurrentSiteId = _sessionContext.SiteId;

        if (clientIdFilter.HasValue && clientIdFilter.Value > 0)
            context.CurrentClientId = clientIdFilter.Value;
        else
            context.CurrentClientId = _sessionContext.ClientId;

        var statusId = (int)status;
        var baseQuery = context.Eractivities
            .Where(a => !a.IsCleaned && a.EractivityStatusId == statusId);

        // Distinct CreatedBy user GUIDs from activities in this context
        var createdByGuids = await baseQuery
            .Where(a => a.CreatedBy.HasValue)
            .Select(a => a.CreatedBy!.Value)
            .Distinct()
            .ToListAsync(ct);

        var createdByUsers = await context.Users
            .Where(u => createdByGuids.Contains(u.UserId))
            .OrderBy(u => u.FullName ?? u.UserName)
            .Select(u => new UserDropdownDto
            {
                UserId = u.UserId,
                DisplayName = u.FullName ?? u.UserName ?? ""
            })
            .ToListAsync(ct);

        // Distinct Responsible (Recruitment Responsible) user GUIDs from activities in this context
        var responsibleGuids = await baseQuery
            .Where(a => a.Responsible.HasValue)
            .Select(a => a.Responsible!.Value)
            .Distinct()
            .ToListAsync(ct);

        var responsibleUsers = await context.Users
            .Where(u => responsibleGuids.Contains(u.UserId))
            .OrderBy(u => u.FullName ?? u.UserName)
            .Select(u => new UserDropdownDto
            {
                UserId = u.UserId,
                DisplayName = u.FullName ?? u.UserName ?? ""
            })
            .ToListAsync(ct);

        // Distinct ClientSections used by activities in this context
        var clientSectionIds = await baseQuery
            .Where(a => a.ClientSectionId.HasValue)
            .Select(a => a.ClientSectionId!.Value)
            .Distinct()
            .ToListAsync(ct);

        var clientSections = await context.ClientSections
            .Where(cs => clientSectionIds.Contains(cs.ClientSectionId))
            .OrderBy(cs => cs.Name)
            .Select(cs => new ClientSectionDropdownDto
            {
                ClientSectionId = cs.ClientSectionId,
                Name = cs.Name
            })
            .ToListAsync(ct);

        // Distinct TemplateGroups used by activities in this context
        var templateGroupIds = await baseQuery
            .Where(a => a.ErtemplateGroupId.HasValue)
            .Select(a => a.ErtemplateGroupId!.Value)
            .Distinct()
            .ToListAsync(ct);

        var templateGroups = await context.ErTemplateGroups
            .Where(tg => templateGroupIds.Contains(tg.ErtemplateGroupId))
            .OrderBy(tg => tg.Name)
            .Select(tg => new TemplateGroupDropdownDto
            {
                TemplateGroupId = tg.ErtemplateGroupId,
                Name = tg.Name
            })
            .ToListAsync(ct);

        return new ActivityFilterOptionsDto
        {
            CreatedByUsers = createdByUsers,
            RecruitmentResponsibleUsers = responsibleUsers,
            ClientSections = clientSections,
            TemplateGroups = templateGroups
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

}
