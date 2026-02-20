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
public class ErActivityService : IErActivityService
{
    private readonly IDbContextFactory<SignaturDbContext> _contextFactory;
    private readonly IUserSessionContext _sessionContext;
    private readonly IPermissionService _permissionService;
    private readonly ICurrentUserService _currentUserService;

    public ErActivityService(
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
        int draftAreaTypeId = 0,
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

        // Load all user permissions once (cached within this scope by IPermissionService)
        var userPermissions = await _permissionService.GetUserPermissionsAsync(_sessionContext.UserName, ct);
        bool hasRecruitmentAccess = userPermissions.Contains((int)PortalPermission.RecruitmentPortalRecruitmentAccess);

        // Mirrors legacy PermissionHelper.UserCanAccessActivitiesUserNotMemberOf
        bool canViewActivitiesNotMemberOf = hasRecruitmentAccess
            && (userPermissions.Contains((int)PortalPermission.RecruitmentPortalViewActivitiesUserNotMemberOf)
                || userPermissions.Contains((int)PortalPermission.RecruitmentPortalEditActivitiesUserNotMemberOf));

        // Mirrors legacy PermissionHelper.UserCanAccessActivitiesWithWorkAreaUserNotMemberOf
        bool canViewActivitiesFromWorkAreaNotMemberOf = hasRecruitmentAccess
            && (userPermissions.Contains((int)PortalPermission.RecruitmentPortalViewActivitiesFromWorkAreaUserNotMemberOf)
                || userPermissions.Contains((int)PortalPermission.RecruitmentPortalEditActivitiesFromWorkAreaUserNotMemberOf));

        System.Diagnostics.Debug.WriteLine($"[DEBUG] UserGuid={currentUserGuid}, CanViewNotMemberOf={canViewActivitiesNotMemberOf}, CanViewFromWorkArea={canViewActivitiesFromWorkAreaNotMemberOf}");

        // Build base query.
        // IsCleaned activities are included — they render with gray italic styling (activity-row-cleaned).
        // Matches legacy ActivityList.ascx.cs which shows cleaned activities in the OnGoing list.
        var query = context.Eractivities.AsQueryable();

        // Apply permission-based visibility filtering (mirrors legacy ActivityList.aspx LoadFiltersData)
        if (!canViewActivitiesNotMemberOf && currentUserGuid.HasValue)
        {
            // Filter 1 (most restrictive): user may only see activities where they are a member, creator, or responsible
            query = query.Where(a =>
                a.Eractivitymembers.Any(m => m.UserId == currentUserGuid.Value) ||
                a.CreatedBy == currentUserGuid.Value ||
                a.Responsible == currentUserGuid.Value);
        }
        else if (!canViewActivitiesFromWorkAreaNotMemberOf && _sessionContext.IsClientUser && currentUserGuid.HasValue)
        {
            // Filter 2: client user restricted to their template groups — but still sees activities
            // where they are a member/creator/responsible, and activities with no template group
            var userTemplateGroupIds = await context.Database
                .SqlQueryRaw<int>(
                    "SELECT TemplateGroupId FROM UserRecruitmentTemplateGroup WHERE UserId = {0}",
                    currentUserGuid.Value)
                .ToListAsync(ct);

            if (userTemplateGroupIds.Count > 0)
            {
                query = query.Where(a =>
                    a.Eractivitymembers.Any(m => m.UserId == currentUserGuid.Value) ||
                    a.CreatedBy == currentUserGuid.Value ||
                    a.Responsible == currentUserGuid.Value ||
                    !a.ErtemplateGroupId.HasValue ||
                    a.ErtemplateGroupId.Value <= 0 ||
                    userTemplateGroupIds.Contains(a.ErtemplateGroupId.Value));
            }
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

            if (moreFilters.ClientSectionGroupId.HasValue)
                query = query.Where(a => a.ClientSection != null && a.ClientSection.ClientSectionGroupId == moreFilters.ClientSectionGroupId.Value);

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
            ["DraftAreaName"] = null,
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
                TemplateGroupName = a.ErTemplateGroup != null ? a.ErTemplateGroup.Name : "",
                // DraftAreaName: populated via post-query logic below (draftAreaTypeId 1=ClientSection, 2=ERTemplateGroup)
                DraftAreaName = "",
                // Row-coloring fields — IsUserMember resolved here; candidate counts populated post-query via raw SQL
                IsCleaned = a.IsCleaned,
                CandidateEvaluationEnabled = a.CandidateEvaluationEnabled,
                IsUserMember = currentUserGuid.HasValue && a.Eractivitymembers.Any(m => m.UserId == currentUserGuid.Value),
                CandidateMissingEvaluationCount = 0,
                CandidateNotReadCount = 0
            })
            .ToListAsync(ct);

        // Populate DraftAreaName based on draftAreaTypeId:
        // Type 2 (ERTemplateGroup): reuse the already-resolved TemplateGroupName.
        // Type 1 (ClientSection): traverse the ClientSection hierarchy to find the top-level root name.
        //   Uses a recursive CTE — mirrors legacy HelperERecruiting.ERForListGet includeClientSectionTopLevel logic.
        //   Activity IDs are integers from our own filtered query, so inline IN list is safe.
        if (draftAreaTypeId == 2 && items.Count > 0)
        {
            items = items
                .Select(item => item with { DraftAreaName = item.TemplateGroupName })
                .ToList();
        }
        else if (draftAreaTypeId == 1 && items.Count > 0)
        {
            var idCsv = string.Join(",", items.Select(item => item.EractivityId.ToString("D")));
#pragma warning disable EF1002 // idCsv contains only integer literals from our own paginated query — not user input
            var topLevelRows = await context.Database
                .SqlQueryRaw<ActivityTopLevelSectionRow>($"""
                    WITH CTE AS (
                        SELECT ERA.ERActivityId, CS.Name, CS.ParentClientSectionId
                        FROM ERActivity ERA
                        LEFT JOIN ClientSection CS ON ERA.ClientSectionId = CS.ClientSectionId
                        WHERE ERA.ERActivityId IN ({idCsv})
                        UNION ALL
                        SELECT CTE.ERActivityId, CS.Name, CS.ParentClientSectionId
                        FROM CTE
                        INNER JOIN ClientSection CS ON CS.ClientSectionId = CTE.ParentClientSectionId
                    )
                    SELECT CTE.ERActivityId, CTE.Name AS TopLevelName
                    FROM CTE
                    WHERE CTE.ParentClientSectionId IS NULL
                    """)
                .ToListAsync(ct);
#pragma warning restore EF1002

            var topLevelMap = topLevelRows.ToDictionary(r => r.ERActivityId, r => r.TopLevelName ?? "");
            items = items
                .Select(item => item with
                {
                    DraftAreaName = topLevelMap.TryGetValue(item.EractivityId, out var name) ? name : ""
                })
                .ToList();
        }

        // Populate candidate counts for row color logic.
        // Only computed for OnGoing mode — color applies only there — and only when the user is known.
        // Two raw SQL queries (one per evaluation mode) covering the full page in one round-trip each.
        // Mirrors legacy HelperERecruiting ERForListGet CandidateReadCount / CandidateHasEvaluationCount queries.
        if (statusFilter == ERActivityStatus.OnGoing && items.Count > 0 && currentUserGuid.HasValue)
        {
            var userIdStr = currentUserGuid.Value.ToString("D");
            var idCsv = string.Join(",", items.Select(item => item.EractivityId.ToString("D")));

#pragma warning disable EF1002 // idCsv = integers from our own query; userIdStr = Guid (hex+hyphens only) — not user input
            // CandidateNotReadCount: candidates not yet read by the current user, for activities
            // where CandidateEvaluationEnabled = false.
            var unreadRows = await context.Database
                .SqlQueryRaw<ActivityCandidateCountRow>($"""
                    SELECT era.ERActivityId, COUNT(erc.ERCandidateId) AS CandidateCount
                    FROM ERActivity era
                    JOIN ERActivityMember eram ON eram.ERActivityId = era.ERActivityId AND eram.UserId = '{userIdStr}'
                    JOIN ERCandidate erc ON erc.ERActivityId = era.ERActivityId
                        AND erc.IsDeleted = 0
                        AND erc.ERCandidateStatusId NOT IN (3, 4)
                    WHERE era.ERActivityId IN ({idCsv})
                        AND era.CandidateEvaluationEnabled = 0
                        AND NOT EXISTS (
                            SELECT 1 FROM ErCandidateUser ercu
                            WHERE ercu.CandidateId = erc.ERCandidateId AND ercu.UserId = '{userIdStr}'
                        )
                    GROUP BY era.ERActivityId
                    """)
                .ToListAsync(ct);

            // CandidateMissingEvaluationCount: candidates not yet evaluated by the current member, for
            // activities where CandidateEvaluationEnabled = true and NOT using extended evaluation.
            // Extended evaluation (CandidateExtendedEvaluationEnabled = true) requires per-criteria checks
            // across ERCandidateExtendedEvaluation — that path is deferred to Option B icon work.
            var missingEvalRows = await context.Database
                .SqlQueryRaw<ActivityCandidateCountRow>($"""
                    SELECT era.ERActivityId, COUNT(erc.ERCandidateId) AS CandidateCount
                    FROM ERActivity era
                    JOIN ERActivityMember eram ON eram.ERActivityId = era.ERActivityId AND eram.UserId = '{userIdStr}'
                    JOIN ERCandidate erc ON erc.ERActivityId = era.ERActivityId
                        AND erc.IsDeleted = 0
                        AND erc.ERCandidateStatusId NOT IN (3, 4)
                    WHERE era.ERActivityId IN ({idCsv})
                        AND era.CandidateEvaluationEnabled = 1
                        AND era.CandidateExtendedEvaluationEnabled = 0
                        AND NOT EXISTS (
                            SELECT 1 FROM ERCandidateEvaluation erce
                            WHERE erce.ERCandidateId = erc.ERCandidateId
                              AND erce.ERActivityMemberId = eram.ERActivityMemberId
                        )
                    GROUP BY era.ERActivityId
                    """)
                .ToListAsync(ct);
#pragma warning restore EF1002

            var unreadMap = unreadRows.ToDictionary(r => r.ERActivityId, r => r.CandidateCount);
            var missingEvalMap = missingEvalRows.ToDictionary(r => r.ERActivityId, r => r.CandidateCount);

            items = items
                .Select(item => item with
                {
                    CandidateNotReadCount = unreadMap.TryGetValue(item.EractivityId, out var unread) ? unread : 0,
                    CandidateMissingEvaluationCount = missingEvalMap.TryGetValue(item.EractivityId, out var missing) ? missing : 0
                })
                .ToList();
        }

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

        // Distinct ClientSectionGroups from activities in this context (via ClientSection navigation)
        var sectionGroupIds = await baseQuery
            .Where(a => a.ClientSection != null && a.ClientSection.ClientSectionGroupId.HasValue)
            .Select(a => a.ClientSection!.ClientSectionGroupId!.Value)
            .Distinct()
            .ToListAsync(ct);

        var sectionGroups = await context.ClientSectionGroups
            .Where(csg => sectionGroupIds.Contains(csg.ClientSectionGroupId))
            .OrderBy(csg => csg.Name)
            .Select(csg => new ClientSectionGroupDropdownDto
            {
                ClientSectionGroupId = csg.ClientSectionGroupId,
                Name = csg.Name
            })
            .ToListAsync(ct);

        return new ActivityFilterOptionsDto
        {
            CreatedByUsers = createdByUsers,
            RecruitmentResponsibleUsers = responsibleUsers,
            ClientSections = clientSections,
            TemplateGroups = templateGroups,
            ClientSectionGroups = sectionGroups
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
    /// Gets all activity members for the Excel export.
    /// Mirrors the legacy ERActivityAndMembersGet logic:
    ///  - Responsible users get IsMember=true (but IsResponsible stays false — legacy behavior)
    ///  - AlternativeResponsible users get IsResponsibleAlternative=true
    ///  - ERActivityMember rows get IsMember=true
    ///  - Users can appear in multiple categories; flags are merged per (ActivityId, UserId)
    ///  - InRoles: comma-separated ASP.NET role names filtered to RecruitmentPortal (PermissionTypeId=2) for clientId
    /// </summary>
    public async Task<List<ActivityMemberExportRow>> GetActivityMembersForExportAsync(
        int clientId,
        ERActivityStatus status,
        ActivityListFilterDto? moreFilters = null,
        CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        context.CurrentSiteId = _sessionContext.SiteId;
        context.CurrentClientId = clientId;

        // Use UserId directly from session — avoids AuthenticationStateProvider which is
        // only valid inside Blazor circuit scope, not in minimal API request context.
        var currentUserGuid = _sessionContext.UserId;

        var userPermissions = await _permissionService.GetUserPermissionsAsync(_sessionContext.UserName, ct);
        bool hasRecruitmentAccess = userPermissions.Contains((int)PortalPermission.RecruitmentPortalRecruitmentAccess);

        bool canViewActivitiesNotMemberOf = hasRecruitmentAccess
            && (userPermissions.Contains((int)PortalPermission.RecruitmentPortalViewActivitiesUserNotMemberOf)
                || userPermissions.Contains((int)PortalPermission.RecruitmentPortalEditActivitiesUserNotMemberOf));

        bool canViewActivitiesFromWorkAreaNotMemberOf = hasRecruitmentAccess
            && (userPermissions.Contains((int)PortalPermission.RecruitmentPortalViewActivitiesFromWorkAreaUserNotMemberOf)
                || userPermissions.Contains((int)PortalPermission.RecruitmentPortalEditActivitiesFromWorkAreaUserNotMemberOf));

        var statusId = (int)status;

        var activityQuery = context.Eractivities
            .Where(a => !a.IsCleaned && a.EractivityStatusId == statusId);

        if (!canViewActivitiesNotMemberOf && currentUserGuid.HasValue)
        {
            activityQuery = activityQuery.Where(a =>
                a.Eractivitymembers.Any(m => m.UserId == currentUserGuid.Value) ||
                a.CreatedBy == currentUserGuid.Value ||
                a.Responsible == currentUserGuid.Value);
        }
        else if (!canViewActivitiesFromWorkAreaNotMemberOf && _sessionContext.IsClientUser && currentUserGuid.HasValue)
        {
            var userTemplateGroupIds = await context.Database
                .SqlQueryRaw<int>(
                    "SELECT TemplateGroupId FROM UserRecruitmentTemplateGroup WHERE UserId = {0}",
                    currentUserGuid.Value)
                .ToListAsync(ct);

            if (userTemplateGroupIds.Count > 0)
            {
                activityQuery = activityQuery.Where(a =>
                    a.Eractivitymembers.Any(m => m.UserId == currentUserGuid.Value) ||
                    a.CreatedBy == currentUserGuid.Value ||
                    a.Responsible == currentUserGuid.Value ||
                    !a.ErtemplateGroupId.HasValue ||
                    a.ErtemplateGroupId.Value <= 0 ||
                    userTemplateGroupIds.Contains(a.ErtemplateGroupId.Value));
            }
        }

        if (moreFilters != null)
        {
            if (moreFilters.CreatedByUserId.HasValue)
                activityQuery = activityQuery.Where(a => a.CreatedBy == moreFilters.CreatedByUserId.Value);
            if (moreFilters.RecruitmentResponsibleUserId.HasValue)
                activityQuery = activityQuery.Where(a => a.Responsible == moreFilters.RecruitmentResponsibleUserId.Value);
            if (moreFilters.ClientSectionId.HasValue)
                activityQuery = activityQuery.Where(a => a.ClientSectionId == moreFilters.ClientSectionId.Value);
            if (moreFilters.TemplateGroupId.HasValue)
                activityQuery = activityQuery.Where(a => a.ErtemplateGroupId == moreFilters.TemplateGroupId.Value);
            if (moreFilters.ClientSectionGroupId.HasValue)
                activityQuery = activityQuery.Where(a => a.ClientSection != null && a.ClientSection.ClientSectionGroupId == moreFilters.ClientSectionGroupId.Value);
            if (moreFilters.DateFrom.HasValue)
                activityQuery = activityQuery.Where(a => a.CreateDate >= moreFilters.DateFrom.Value.Date);
            if (moreFilters.DateTo.HasValue)
                activityQuery = activityQuery.Where(a => a.CreateDate < moreFilters.DateTo.Value.Date.AddDays(1));
        }

        var activities = await activityQuery
            .Select(a => new { a.EractivityId, a.Responsible })
            .OrderBy(a => a.EractivityId)
            .ToListAsync(ct);

        if (activities.Count == 0)
            return [];

        var activityIds = activities.Select(a => a.EractivityId).ToList();

        // Roles: aspnet_Roles for this client, active, that grant any RecruitmentPortal permission (PermissionTypeId = 2)
        const int recruitmentPortalTypeId = 2;
        var roleAssignments = await context.AspnetRoles
            .Where(r => r.ClientId == clientId && r.IsActive &&
                        r.PermissionInRoles.Any(pir => pir.Permission.PermissionTypeId == recruitmentPortalTypeId))
            .SelectMany(r => r.Users, (role, user) => new { user.UserId, role.RoleName })
            .ToListAsync(ct);

        var userRolesDict = roleAssignments
            .GroupBy(x => x.UserId)
            .ToDictionary(g => g.Key, g => string.Join(", ", g.Select(x => x.RoleName).OrderBy(r => r)));

        // Responsible user data
        var responsibleIds = activities
            .Where(a => a.Responsible.HasValue)
            .Select(a => a.Responsible!.Value)
            .Distinct()
            .ToList();

        var responsibleDataById = new Dictionary<Guid, (string FullName, string Email, bool IsInternal, bool IsActive)>();
        if (responsibleIds.Count > 0)
        {
            var responsibleUsers = await context.Users
                .Where(u => responsibleIds.Contains(u.UserId))
                .Select(u => new { u.UserId, u.FullName, u.Email, u.IsInternal, IsActive = u.Enabled ?? false })
                .ToListAsync(ct);
            responsibleDataById = responsibleUsers.ToDictionary(
                u => u.UserId,
                u => (u.FullName ?? "", u.Email ?? "", u.IsInternal, u.IsActive));
        }

        // AlternativeResponsible users
        var altResponsibles = await context.EractivityAlternativeResponsibles
            .Where(ar => activityIds.Contains(ar.EractivityId))
            .Select(ar => new
            {
                ar.EractivityId,
                ar.UserId,
                FullName = ar.User.FullName ?? "",
                Email = ar.User.Email ?? "",
                ar.User.IsInternal,
                IsActive = ar.User.Enabled ?? false
            })
            .ToListAsync(ct);

        var altResponsibleLookup = altResponsibles
            .GroupBy(x => x.EractivityId)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Committee members (ERActivityMember)
        var committeeMembers = await context.Eractivitymembers
            .Where(m => activityIds.Contains(m.EractivityId))
            .Select(m => new
            {
                m.EractivityId,
                m.UserId,
                FullName = m.User.FullName ?? "",
                Email = m.User.Email ?? "",
                m.User.IsInternal,
                IsActive = m.User.Enabled ?? false
            })
            .ToListAsync(ct);

        var committeeMemberLookup = committeeMembers
            .GroupBy(x => x.EractivityId)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Merge into flat export rows — mirrors legacy ERActivityAndMembersGet merge logic
        var rows = new List<ActivityMemberExportRow>();
        foreach (var activity in activities)
        {
            // (userId) → (FullName, Email, IsInternal, IsActive, IsAltResponsible, IsMember)
            var memberMap = new Dictionary<Guid, (string FullName, string Email, bool IsInternal, bool IsActive, bool IsAltResponsible, bool IsMember)>();

            // 1. Responsible user → IsMember=true (legacy: Table[1] sets IsMember=true for Responsible)
            if (activity.Responsible.HasValue && responsibleDataById.TryGetValue(activity.Responsible.Value, out var respData))
                memberMap[activity.Responsible.Value] = (respData.FullName, respData.Email, respData.IsInternal, respData.IsActive, false, true);

            // 2. AlternativeResponsible users → IsAltResponsible=true; merge if already in map
            if (altResponsibleLookup.TryGetValue(activity.EractivityId, out var altList))
            {
                foreach (var ar in altList)
                {
                    if (memberMap.TryGetValue(ar.UserId, out var existing))
                        memberMap[ar.UserId] = (existing.FullName, existing.Email, existing.IsInternal, existing.IsActive, true, existing.IsMember);
                    else
                        memberMap[ar.UserId] = (ar.FullName, ar.Email, ar.IsInternal, ar.IsActive, true, false);
                }
            }

            // 3. Committee members → IsMember=true; merge if already in map
            if (committeeMemberLookup.TryGetValue(activity.EractivityId, out var memberList))
            {
                foreach (var cm in memberList)
                {
                    if (memberMap.TryGetValue(cm.UserId, out var existing))
                        memberMap[cm.UserId] = (existing.FullName, existing.Email, existing.IsInternal, existing.IsActive, existing.IsAltResponsible, true);
                    else
                        memberMap[cm.UserId] = (cm.FullName, cm.Email, cm.IsInternal, cm.IsActive, false, true);
                }
            }

            foreach (var (userId, info) in memberMap.OrderBy(kvp => kvp.Value.FullName))
            {
                rows.Add(new ActivityMemberExportRow
                {
                    ActivityId = activity.EractivityId,
                    FullName = info.FullName,
                    Email = info.Email,
                    InRoles = userRolesDict.GetValueOrDefault(userId, ""),
                    IsResponsible = false, // Legacy never sets IsResponsible=true in ERActivityAndMembersGet
                    IsResponsibleAlternative = info.IsAltResponsible,
                    IsMember = info.IsMember,
                    IsInternal = info.IsInternal,
                    IsActive = info.IsActive
                });
            }
        }

        return rows;
    }

    /// <summary>
    /// Writes a UserActivityLog entry recording that an external user was force-logged out
    /// because they have no active recruitment activities.
    /// Mirrors legacy: HelperERecruiting.UserActivityLogCreate(siteId, userId, "Tvunget logget ud...", null).
    /// </summary>
    public async Task LogExternalUserForceLogoutAsync(Guid userId, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        context.UserActivityLogs.Add(new Infrastructure.Data.Entities.UserActivityLog
        {
            ActionUserId = userId,
            TimeStamp = DateTime.Now,
            Log = "Tvunget logget ud. Ekstern bruger er ikke medlem af nogle aktive sager."
        });

        await context.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Writes a UserActivityLog entry recording that an external user was force-logged out
    /// because their client does not have the recruitment portal enabled.
    /// Mirrors legacy ActivityList.aspx.cs:286.
    /// </summary>
    public async Task LogClientNoRecruitmentPortalForceLogoutAsync(Guid userId, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        context.UserActivityLogs.Add(new Infrastructure.Data.Entities.UserActivityLog
        {
            ActionUserId = userId,
            TimeStamp = DateTime.Now,
            Log = "Tvunget logget ud. Kunde har ikke rekrutteringsportal tilsluttet."
        });

        await context.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Counts active (OnGoing) activities the given user is associated with
    /// as a member, creator, or responsible person.
    // Result type for the Draft Area ClientSection top-level hierarchy CTE query.
    private class ActivityTopLevelSectionRow
    {
        public int ERActivityId { get; set; }
        public string? TopLevelName { get; set; }
    }

    // Result type for the candidate count raw SQL queries (unread / missing evaluation).
    private class ActivityCandidateCountRow
    {
        public int ERActivityId { get; set; }
        public int CandidateCount { get; set; }
    }

    /// Mirrors legacy HelperERecruiting.UserInActiveActivitiesCount().
    /// </summary>
    public async Task<int> GetUserActiveActivitiesCountAsync(Guid userId, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        context.CurrentSiteId = _sessionContext.SiteId;
        context.CurrentClientId = _sessionContext.ClientId;

        var ongoingStatusId = (int)ERActivityStatus.OnGoing;

        return await context.Eractivities
            .Where(a => !a.IsCleaned &&
                        a.EractivityStatusId == ongoingStatusId &&
                        (a.Responsible == userId ||
                         a.CreatedBy == userId ||
                         a.Eractivitymembers.Any(m => m.UserId == userId)))
            .CountAsync(ct);
    }

}
