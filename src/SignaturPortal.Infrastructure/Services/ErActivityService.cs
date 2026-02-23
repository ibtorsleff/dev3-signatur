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
    private readonly ILocalizationService _localization;

    public ErActivityService(
        IDbContextFactory<SignaturDbContext> contextFactory,
        IUserSessionContext sessionContext,
        IPermissionService permissionService,
        ICurrentUserService currentUserService,
        ILocalizationService localization)
    {
        _contextFactory = contextFactory;
        _sessionContext = sessionContext;
        _permissionService = permissionService;
        _currentUserService = currentUserService;
        _localization = localization;
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
        bool includeEmailWarning = false,
        bool includeWebAdStatus = false,
        bool includeWebAdChanges = false,
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
                // Active candidates: not deleted AND not Hired(3)/Rejected(4) — matches legacy CandidateTotalCount
                ActiveCandidateCount = a.Ercandidates.Count(c => !c.IsDeleted && c.ErcandidateStatusId != 3 && c.ErcandidateStatusId != 4),
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
                WebAdId = a.WebAdId,
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

#pragma warning disable EF1002 // idCsv = integers from our own query; userIdStr = Guid (hex+hyphens only) — not user input
            if (includeEmailWarning)
            {
                var emailWarningRows = await context.Database
                    .SqlQueryRaw<ActivityEmailWarningRow>($"""
                        SELECT era.ERActivityId,
                               COUNT(eram.ERActivityMemberId) AS MembersMissingNotificationEmail
                        FROM ERActivity era
                        LEFT JOIN ERActivityMember eram
                            ON eram.ERActivityId = era.ERActivityId
                            AND eram.NotificationMailSendToUser = 0
                            AND eram.UserId <> era.CreatedBy
                        WHERE era.ERActivityId IN ({idCsv})
                        GROUP BY era.ERActivityId
                        """)
                    .ToListAsync(ct);

                var emailWarningMap = emailWarningRows
                    .ToDictionary(r => r.ERActivityId, r => r.MembersMissingNotificationEmail);

                items = items
                    .Select(item => item with
                    {
                        MembersMissingNotificationEmail = emailWarningMap
                            .TryGetValue(item.EractivityId, out var count) ? count : 0
                    })
                    .ToList();
            }

            if (includeWebAdStatus)
            {
                var webAdRows = await context.Database
                    .SqlQueryRaw<ActivityWebAdStatusRow>($"""
                        SELECT
                            era.ERActivityId,
                            CAST(CASE WHEN era.ActivityId IS NOT NULL AND era.ActivityId > 0 AND
                                (SELECT COUNT(*) FROM Insertion ins
                                 INNER JOIN Media med ON ins.MediaId = med.MediaId
                                 WHERE ins.ActivityId = era.ActivityId
                                   AND med.MediaSpecialFunctionId IN (1, 3, 4, 5, 6)) > 0
                            THEN 1 ELSE 0 END AS BIT) AS HasWebAdMedia,
                            CAST(CASE WHEN era.ActivityId IS NOT NULL AND era.ActivityId > 0 AND
                                (SELECT COUNT(*) FROM Insertion ins
                                 INNER JOIN Media med ON ins.MediaId = med.MediaId
                                 WHERE ins.ActivityId = era.ActivityId
                                   AND med.MediaSpecialFunctionId IN (2, 3, 6)) > 0
                            THEN 1 ELSE 0 END AS BIT) AS HasJobnetMedia,
                            wa.WebAdStatusId,
                            jwa.WebAdId AS JobnetWebAdId,
                            jwa.JobnetStatusId
                        FROM ERActivity era
                        LEFT JOIN WebAd wa ON wa.WebAdId = era.WebAdId
                        LEFT JOIN JobnetWebAd jwa ON jwa.WebAdId = era.WebAdId
                        WHERE era.ERActivityId IN ({idCsv})
                        """)
                    .ToListAsync(ct);

                var webAdMap = webAdRows.ToDictionary(r => r.ERActivityId);

                items = items
                    .Select(item =>
                    {
                        if (!webAdMap.TryGetValue(item.EractivityId, out var wa)) return item;
                        return item with
                        {
                            HasWebAdMedia = wa.HasWebAdMedia,
                            HasJobnetMedia = wa.HasJobnetMedia,
                            WebAdStatusId = wa.WebAdStatusId,
                            JobnetWebAdId = wa.JobnetWebAdId,
                            JobnetStatusId = wa.JobnetStatusId,
                        };
                    })
                    .ToList();
            }

            if (includeWebAdChanges && currentUserGuid.HasValue)
            {
                var changesRows = await context.Database
                    .SqlQueryRaw<ActivityWebAdChangeRow>($"""
                        SELECT era.ERActivityId, wafc.FieldName, wafc.IsMail, wafc.ExtraData
                        FROM ERActivity era
                        INNER JOIN WebAd wa ON wa.WebAdId = era.WebAdId
                        INNER JOIN WebAdFieldChange wafc ON wafc.WebAdId = wa.WebAdId
                        WHERE era.ERActivityId IN ({idCsv})
                          AND wafc.UserId <> '{userIdStr}'
                        ORDER BY era.ERActivityId, wafc.[SortOrder], wafc.TimeStamp
                        """)
                    .ToListAsync(ct);

                var changesMap = changesRows
                    .GroupBy(r => r.ERActivityId)
                    .ToDictionary(g => g.Key, g => BuildWebAdChangeSummary(g.ToList()));

                items = items
                    .Select(item =>
                    {
                        if (!changesMap.TryGetValue(item.EractivityId, out var summary)) return item;
                        return item with
                        {
                            HasWebAdChanges = true,
                            WebAdChangeSummary = summary,
                        };
                    })
                    .ToList();
            }
#pragma warning restore EF1002
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

    // Result type for Icon 1 — email warning count per activity.
    private class ActivityEmailWarningRow
    {
        public int ERActivityId { get; set; }
        public int MembersMissingNotificationEmail { get; set; }
    }

    // Result type for Icon 2 — web ad status per activity.
    private class ActivityWebAdStatusRow
    {
        public int ERActivityId { get; set; }
        public bool HasWebAdMedia { get; set; }
        public bool HasJobnetMedia { get; set; }
        public int? WebAdStatusId { get; set; }
        public int? JobnetWebAdId { get; set; }
        public int? JobnetStatusId { get; set; }
    }

    // Result type for Icon 3 — web ad field changes per activity.
    internal class ActivityWebAdChangeRow
    {
        public int ERActivityId { get; set; }
        public string FieldName { get; set; } = "";
        public bool IsMail { get; set; }
        public string? ExtraData { get; set; }
    }

    // Builds a newline-separated list of changed field names for the web ad changes icon tooltip.
    // Skips mail-only changes and deduplicates by field name.
    // The component adds the localized header text; this returns only the raw field names.
    internal static string BuildWebAdChangeSummary(List<ActivityWebAdChangeRow> changes)
    {
        var sb = new System.Text.StringBuilder();
        var seenFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in changes)
        {
            if (row.IsMail) continue;
            if (!seenFields.Add(row.FieldName)) continue;
            if (sb.Length > 0) sb.Append('\n');
            sb.Append(row.FieldName);
        }
        return sb.ToString();
    }

    /// Mirrors legacy HelperERecruiting.UserInActiveActivitiesCount().
    /// </summary>
    public async Task<int> GetUserActiveActivitiesCountAsync(Guid userId, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        context.CurrentSiteId = _sessionContext.SiteId;
        context.CurrentClientId = _sessionContext.ClientId;

        var ongoingStatusId = (int)ERActivityStatus.OnGoing;

        // Mirror the same visibility logic as GetActivitiesAsync so the count reflects
        // exactly what the grid shows. Users with ViewActivitiesUserNotMemberOf see ALL
        // client activities — applying a membership filter here would return 0 for them.
        var userPermissions = await _permissionService.GetUserPermissionsAsync(_sessionContext.UserName, ct);
        bool hasRecruitmentAccess = userPermissions.Contains((int)PortalPermission.RecruitmentPortalRecruitmentAccess);
        bool canViewActivitiesNotMemberOf = hasRecruitmentAccess
            && (userPermissions.Contains((int)PortalPermission.RecruitmentPortalViewActivitiesUserNotMemberOf)
                || userPermissions.Contains((int)PortalPermission.RecruitmentPortalEditActivitiesUserNotMemberOf));

        var query = context.Eractivities
            .Where(a => a.EractivityStatusId == ongoingStatusId);

        if (!canViewActivitiesNotMemberOf)
        {
            query = query.Where(a =>
                a.Responsible == userId ||
                a.CreatedBy == userId ||
                a.Eractivitymembers.Any(m => m.UserId == userId));
        }

        return await query.CountAsync(ct);
    }

    // =========================================================================
    // Form load / save
    // =========================================================================

    /// <summary>
    /// Loads full activity data for pre-populating the ActivityCreateEdit form in edit mode.
    /// </summary>
    public async Task<ActivityEditDto?> GetActivityForEditAsync(int activityId, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        context.CurrentSiteId = _sessionContext.SiteId;
        context.CurrentClientId = _sessionContext.ClientId;

        var activity = await context.Eractivities
            .Where(a => a.EractivityId == activityId && !a.IsCleaned)
            .Select(a => new
            {
                a.EractivityId,
                a.ClientId,
                ClientName = a.Client != null ? a.Client.ObjectData : null,
                a.Headline,
                a.Jobtitle,
                a.EractivityStatusId,
                a.JobnetOccupationId,
                a.ClientSectionId,
                ClientSectionName = a.ClientSection != null ? a.ClientSection.Name : null,
                ClientSectionGroupId = a.ClientSection != null ? a.ClientSection.ClientSectionGroupId : null,
                a.IsReposting,
                a.IsLeadershipPosition,
                a.IsBlindRecruitment,
                a.LockCandidateEvalutionAndNotesWhenUserHasNotEvaluated,
                a.ViewRecruitmentCommitteeEvaluations,
                a.EmailOnNewCandidate,
                a.SendDailyStatusEmailEnabled,
                a.ContinuousPosting,
                a.ApplicationDeadline,
                a.HireDate,
                a.HireDateFreeText,
                a.InterviewRounds,
                a.CalendarTypeId,
                a.InterviewDuration,
                a.CandidateExtendedEvaluationEnabled,
                a.SendSmsInterviewRemembrances,
                a.ErtemplateGroupId,
                a.ErapplicationTemplateId,
                a.ErletterTemplateReceivedId,
                a.ErletterTemplateInterviewId,
                a.ErletterTemplateInterviewTwoPlusRoundsId,
                a.ErletterTemplateRejectedId,
                a.ErletterTemplateRejectedAfterInterviewId,
                a.ErnotifyRecruitmentCommitteeId,
                a.ErsmsTemplateInterviewId,
                a.Responsible,
                a.CreatedBy,
                a.CreateDate,
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
                        m.User.FullName,
                        m.User.Email,
                        m.User.UserName
                    })
                    .ToList()
            })
            .AsSplitQuery()
            .FirstOrDefaultAsync(ct);

        if (activity == null) return null;

        // Load alternative responsibles
        var altResponsibles = await context.EractivityAlternativeResponsibles
            .Where(ar => ar.EractivityId == activityId)
            .Select(ar => new UserDropdownDto
            {
                UserId = ar.UserId,
                DisplayName = ar.User.FullName ?? ar.User.UserName ?? ""
            })
            .ToListAsync(ct);

        // Resolve responsible user name
        string? responsibleName = null;
        if (activity.Responsible.HasValue)
        {
            var responsible = await context.Users
                .Where(u => u.UserId == activity.Responsible.Value)
                .Select(u => new { u.FullName, u.UserName })
                .FirstOrDefaultAsync(ct);
            responsibleName = responsible?.FullName ?? responsible?.UserName;
        }

        // Resolve created-by name
        string createdByName = "";
        if (activity.CreatedBy.HasValue)
        {
            var creator = await context.Users
                .Where(u => u.UserId == activity.CreatedBy.Value)
                .Select(u => new { u.FullName, u.UserName })
                .FirstOrDefaultAsync(ct);
            createdByName = creator?.FullName ?? creator?.UserName ?? "";
        }

        // Determine HireDateType: 1=Date, 2=FreeText, 0=None
        int hireDateType = 0;
        if (activity.HireDate.HasValue) hireDateType = 1;
        else if (!string.IsNullOrEmpty(activity.HireDateFreeText)) hireDateType = 2;

        // Map IsLeadershipPosition bool? → int (1=Yes, 2=No, null=not set)
        int? leadershipPositionId = activity.IsLeadershipPosition.HasValue
            ? (activity.IsLeadershipPosition.Value ? 1 : 2)
            : null;

        // Map IsBlindRecruitment bool? → int (1=Yes, 2=No, null=not set)
        int? blindRecruitmentId = activity.IsBlindRecruitment.HasValue
            ? (activity.IsBlindRecruitment.Value ? 1 : 2)
            : null;

        var committeeMembers = activity.HiringTeamMembers
            .Select(m => new HiringTeamMemberDto
            {
                EractivityMemberId = m.EractivityMemberId,
                UserId = m.UserId,
                FullName = m.FullName ?? "",
                Email = m.Email ?? "",
                UserName = m.UserName ?? "",
                MemberTypeId = m.EractivityMemberTypeId,
                MemberTypeName = StatusMappings.GetActivityMemberTypeName(m.EractivityMemberTypeId),
                AllowCandidateManagement = m.ExtUserAllowCandidateManagement,
                AllowCandidateReview = m.ExtUserAllowCandidateReview,
                AllowViewEditNotes = m.ExtUserAllowViewEditNotes,
                NotificationMailSendToUser = m.NotificationMailSendToUser
            })
            .ToList();

        return new ActivityEditDto
        {
            EractivityId = activity.EractivityId,
            ClientId = activity.ClientId,
            CreateDate = activity.CreateDate,
            CreatedByName = createdByName,
            StatusId = activity.EractivityStatusId,
            Headline = activity.Headline,
            JobTitle = activity.Jobtitle,
            JobnetOccupationId = activity.JobnetOccupationId,
            ClientSectionGroupId = activity.ClientSectionGroupId,
            ClientSectionId = activity.ClientSectionId,
            ClientSectionName = activity.ClientSectionName,
            Reposting = activity.IsReposting,
            LeadershipPositionId = leadershipPositionId,
            BlindRecruitmentId = blindRecruitmentId,
            LockCandidateEvaluation = activity.LockCandidateEvalutionAndNotesWhenUserHasNotEvaluated,
            ViewCommitteeEvaluations = activity.ViewRecruitmentCommitteeEvaluations,
            SendEmailOnNewCandidate = activity.EmailOnNewCandidate,
            SendDailyStatusMail = activity.SendDailyStatusEmailEnabled,
            ContinuousPosting = activity.ContinuousPosting,
            ApplicationDeadline = activity.ContinuousPosting ? null : activity.ApplicationDeadline,
            HireDateType = hireDateType,
            HireDate = activity.HireDate,
            HireDateFreeText = activity.HireDateFreeText,
            InterviewRounds = activity.InterviewRounds == 0 ? null : activity.InterviewRounds,
            CalendarTypeId = activity.CalendarTypeId == 0 ? null : activity.CalendarTypeId,
            InterviewDurationId = activity.InterviewDuration,
            ExtendedEvaluationEnabled = activity.CandidateExtendedEvaluationEnabled,
            SendSmsInterviewReminders = activity.SendSmsInterviewRemembrances,
            TemplateGroupId = activity.ErtemplateGroupId,
            ApplicationTemplateId = activity.ErapplicationTemplateId == 0 ? null : activity.ErapplicationTemplateId,
            EmailTemplateReceivedId = activity.ErletterTemplateReceivedId == 0 ? null : activity.ErletterTemplateReceivedId,
            EmailTemplateInterviewId = activity.ErletterTemplateInterviewId == 0 ? null : activity.ErletterTemplateInterviewId,
            EmailTemplateInterview2PlusId = activity.ErletterTemplateInterviewTwoPlusRoundsId,
            EmailTemplateRejectedId = activity.ErletterTemplateRejectedId == 0 ? null : activity.ErletterTemplateRejectedId,
            EmailTemplateRejectedAfterInterviewId = activity.ErletterTemplateRejectedAfterInterviewId == 0 ? null : activity.ErletterTemplateRejectedAfterInterviewId,
            EmailTemplateNotifyCommitteeId = activity.ErnotifyRecruitmentCommitteeId == 0 ? null : activity.ErnotifyRecruitmentCommitteeId,
            SmsTemplateInterviewId = activity.ErsmsTemplateInterviewId,
            RecruitmentResponsibleUserId = activity.Responsible,
            RecruitmentResponsibleName = responsibleName,
            AlternativeResponsibles = altResponsibles,
            CommitteeMembers = committeeMembers
        };
    }

    /// <summary>
    /// Loads all dropdown option lists for the ActivityCreateEdit form for the given client.
    /// Uses raw SQL for legacy lookup tables not yet mapped to EF entities.
    /// </summary>
    public async Task<ActivityFormOptionsDto> GetActivityFormOptionsAsync(
        int clientId,
        int? currentTemplateGroupId = null,
        CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        context.CurrentSiteId = _sessionContext.SiteId;

        // Statuses available on the form (not Draft since draft mode is out of scope)
        var statuses = new List<ActivityStatusOptionDto>
        {
            new(1, StatusMappings.GetActivityStatusName(1)), // Ongoing
            new(2, StatusMappings.GetActivityStatusName(2)), // Closed
            new(3, StatusMappings.GetActivityStatusName(3)), // Deleted
        };

        // Leadership position options (maps to IsLeadershipPosition bool)
        var leadershipPositions = new List<SimpleOptionDto>
        {
            new(1, "Ja"),
            new(2, "Nej")
        };

        // Blind recruitment options (maps to IsBlindRecruitment bool)
        var blindRecruitmentOptions = new List<SimpleOptionDto>
        {
            new(1, "Ja"),
            new(2, "Nej")
        };

        // Interview rounds options (1-5)
        var interviewRoundsOptions = Enumerable.Range(1, 5)
            .Select(i => new SimpleOptionDto(i, i.ToString()))
            .ToList();

        // Template groups for this client (active only)
        var templateGroups = await context.ErTemplateGroups
            .Where(tg => context.Eractivities.Any(a =>
                a.ClientId == clientId &&
                a.ErtemplateGroupId == tg.ErtemplateGroupId))
            .OrderBy(tg => tg.Name)
            .Select(tg => new TemplateGroupDropdownDto
            {
                TemplateGroupId = tg.ErtemplateGroupId,
                Name = tg.Name
            })
            .ToListAsync(ct);

        // Client section groups for this client
        var clientSectionGroups = await context.ClientSectionGroups
            .Where(csg => csg.ClientId == clientId)
            .OrderBy(csg => csg.Name)
            .Select(csg => new ClientSectionGroupDropdownDto
            {
                ClientSectionGroupId = csg.ClientSectionGroupId,
                Name = csg.Name
            })
            .ToListAsync(ct);

        // Jobnet occupations — raw SQL (JobnetOccupation table)
        var jobnetOccupations = await context.Database
            .SqlQueryRaw<SimpleOptionDto>(
                @"SELECT Id, Name
                  FROM JobnetOccupation
                  WHERE Deleted = 0
                  ORDER BY Name")
            .ToListAsync(ct);

        // Recruitment types — hardcoded enum (no DB table; mirrors legacy RecruitmentTypeEn)
        var langId = _sessionContext.UserLanguageId;
        var recruitmentTypes = new List<SimpleOptionDto>
        {
            new((int)ERRecruitmentType.Normal,            _localization.GetText("Normal",            langId)),
            new((int)ERRecruitmentType.LeadershipPosition, _localization.GetText("LeadershipPosition", langId)),
            new((int)ERRecruitmentType.BlindRecruitment,  _localization.GetText("BlindRecruitment",  langId)),
        };

        // Calendar types — hardcoded enum (no DB table; mirrors legacy ERCalendarType)
        var calendarTypes = new List<SimpleOptionDto>
        {
            new((int)ERCalendarType.NoCalendarFunction, _localization.GetText("NoCalendarFunction", langId)),
            new((int)ERCalendarType.OpenCalendar,       _localization.GetText("OpenCalendar",       langId)),
            new((int)ERCalendarType.ClosedCalendar,     _localization.GetText("ClosedCalendar",     langId)),
        };

        // Interview durations — hardcoded list (no DB table; mirrors legacy PopulateInterviewDurations)
        // Id = duration in minutes; Name = localized display string
        var interviewDurations = new List<SimpleOptionDto>
        {
            new(30,  _localization.GetText("XMinutesWithArgs", langId, "30")),
            new(45,  _localization.GetText("XMinutesWithArgs", langId, "45")),
            new(60,  _localization.GetText("XHourWithArgs",    langId, "1")),
            new(75,  _localization.GetText("XHourWithArgs",    langId, "1¼")),
            new(90,  _localization.GetText("XHourWithArgs",    langId, "1½")),
            new(120, _localization.GetText("XHoursWithArgs",   langId, "2")),
            new(150, _localization.GetText("XHoursWithArgs",   langId, "2½")),
            new(180, _localization.GetText("XHoursWithArgs",   langId, "3")),
        };

        // Languages — raw SQL (Languages table; no per-client join table exists)
        var languages = await context.Database
            .SqlQueryRaw<SimpleOptionDto>(
                @"SELECT LanguageID AS Id, RTRIM(LanguageName) AS Name
                  FROM Languages
                  ORDER BY LanguageID")
            .ToListAsync(ct);

        // Application templates for this client (filtered by template group if provided)
        var appTemplatesQuery = currentTemplateGroupId.HasValue
            ? @"SELECT ERApplicationTemplateId AS Id, Name
                FROM ERApplicationTemplate
                WHERE ClientId = {0} AND ERTemplateGroupId = {1}
                  AND IsEnabled = 1 AND ERApplicationTemplateTypeId = 1
                ORDER BY Name"
            : @"SELECT ERApplicationTemplateId AS Id, Name
                FROM ERApplicationTemplate
                WHERE ClientId = {0}
                  AND IsEnabled = 1 AND ERApplicationTemplateTypeId = 1
                ORDER BY Name";

        List<SimpleOptionDto> applicationTemplates;
        if (currentTemplateGroupId.HasValue)
        {
            applicationTemplates = await context.Database
                .SqlQueryRaw<SimpleOptionDto>(appTemplatesQuery, clientId, currentTemplateGroupId.Value)
                .ToListAsync(ct);
        }
        else
        {
            applicationTemplates = await context.Database
                .SqlQueryRaw<SimpleOptionDto>(appTemplatesQuery, clientId)
                .ToListAsync(ct);
        }

        // Email templates for this client — all types from ERLetterTemplate
        // Type 1 = Received, 2 = Interview, 3 = Rejected, 4 = RejectedAfterInterview, 5 = NotifyCommittee
        var emailTemplatesSql = @"SELECT ERLetterTemplateId AS Id, Name
                  FROM ERLetterTemplate
                  WHERE ClientId = {0} AND ERLetterTemplateTypeId = {1}
                    AND IsEnabled = 1
                  ORDER BY Name";

        var emailTemplatesReceived = await context.Database
            .SqlQueryRaw<SimpleOptionDto>(emailTemplatesSql, clientId, 1)
            .ToListAsync(ct);

        var emailTemplatesInterview = await context.Database
            .SqlQueryRaw<SimpleOptionDto>(emailTemplatesSql, clientId, 2)
            .ToListAsync(ct);

        var emailTemplatesRejected = await context.Database
            .SqlQueryRaw<SimpleOptionDto>(emailTemplatesSql, clientId, 3)
            .ToListAsync(ct);

        var emailTemplatesRejectedAfterInterview = await context.Database
            .SqlQueryRaw<SimpleOptionDto>(emailTemplatesSql, clientId, 4)
            .ToListAsync(ct);

        var emailTemplatesNotifyCommittee = await context.Database
            .SqlQueryRaw<SimpleOptionDto>(emailTemplatesSql, clientId, 5)
            .ToListAsync(ct);

        // SMS templates for this client
        var smsTemplates = await context.Database
            .SqlQueryRaw<SimpleOptionDto>(
                @"SELECT ERSmsTemplateId AS Id, Name
                  FROM ERSmsTemplate
                  WHERE ClientId = {0} AND IsEnabled = 1
                  ORDER BY Name",
                clientId)
            .ToListAsync(ct);

        return new ActivityFormOptionsDto
        {
            Statuses = statuses,
            JobnetOccupations = jobnetOccupations,
            ClientSectionGroups = clientSectionGroups,
            LeadershipPositions = leadershipPositions,
            BlindRecruitmentOptions = blindRecruitmentOptions,
            RecruitmentTypes = recruitmentTypes,
            InterviewRoundsOptions = interviewRoundsOptions,
            CalendarTypes = calendarTypes,
            InterviewDurations = interviewDurations,
            Languages = languages,
            TemplateGroups = templateGroups,
            ApplicationTemplates = applicationTemplates,
            EmailTemplatesReceived = emailTemplatesReceived,
            EmailTemplatesInterview = emailTemplatesInterview,
            EmailTemplatesInterview2Plus = emailTemplatesInterview, // same template pool
            EmailTemplatesRejected = emailTemplatesRejected,
            EmailTemplatesRejectedAfterInterview = emailTemplatesRejectedAfterInterview,
            EmailTemplatesNotifyCommittee = emailTemplatesNotifyCommittee,
            SmsTemplates = smsTemplates
        };
    }

    /// <summary>
    /// Live-searches client sections for the MudAutocomplete on the create/edit form.
    /// </summary>
    public async Task<List<ClientSectionDropdownDto>> GetClientSectionsForFormAsync(
        int clientId,
        string? search,
        CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var query = context.ClientSections.AsQueryable();

        // Filter by client via ClientSectionGroup (sections belong to a client via their group,
        // or directly if ClientId column exists). Use raw SQL for accurate client filtering.
        // ClientSection does not have a ClientId FK — filter via the activity history for this client,
        // or rely on ClientSectionGroup.ClientId.
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(cs => cs.Name.Contains(search));
        }

        // Filter sections belonging to this client's section groups
        var clientGroupIds = await context.ClientSectionGroups
            .Where(csg => csg.ClientId == clientId)
            .Select(csg => csg.ClientSectionGroupId)
            .ToListAsync(ct);

        // Include sections from this client's groups OR sections with no group used by this client
        var sections = await context.ClientSections
            .Where(cs => cs.ClientSectionGroupId.HasValue
                ? clientGroupIds.Contains(cs.ClientSectionGroupId.Value)
                : context.Eractivities.Any(a => a.ClientId == clientId && a.ClientSectionId == cs.ClientSectionId))
            .Where(cs => string.IsNullOrWhiteSpace(search) || cs.Name.Contains(search))
            .OrderBy(cs => cs.Name)
            .Take(50)
            .Select(cs => new ClientSectionDropdownDto
            {
                ClientSectionId = cs.ClientSectionId,
                Name = cs.Name
            })
            .ToListAsync(ct);

        return sections;
    }

    /// <summary>
    /// Live-searches users for the UserPickerDialog.
    /// Returns internal users for the site and external users for the given client.
    /// </summary>
    public async Task<List<UserDropdownDto>> GetUsersForPickerAsync(
        int clientId,
        string? search,
        CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var query = context.Users
            .Where(u => u.SiteId == _sessionContext.SiteId
                && u.Enabled == true
                && (u.IsInternal || u.ClientId == clientId));

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(u =>
                (u.FullName != null && u.FullName.Contains(search)) ||
                (u.Email != null && u.Email.Contains(search)) ||
                (u.UserName != null && u.UserName.Contains(search)));
        }

        return await query
            .OrderBy(u => u.FullName ?? u.UserName)
            .Take(50)
            .Select(u => new UserDropdownDto
            {
                UserId = u.UserId,
                DisplayName = u.FullName ?? u.UserName ?? ""
            })
            .ToListAsync(ct);
    }

    /// <summary>
    /// Creates a new activity from the given command.
    /// Returns the new activity's EractivityId.
    /// </summary>
    public async Task<int> CreateActivityAsync(ActivitySaveCommand command, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        context.CurrentSiteId = _sessionContext.SiteId;
        context.CurrentClientId = command.ClientId;

        var entity = MapCommandToEntity(command, new Data.Entities.Eractivity());
        entity.CreateDate = DateTime.Now;
        entity.StatusChangedTimeStamp = DateTime.Now;
        entity.EditedId = Guid.NewGuid();
        entity.CreatedBy = command.SavedByUserId;
        entity.JournalNo = "";
        entity.ApplicationTemplateLanguage = "DK";

        context.Eractivities.Add(entity);
        await context.SaveChangesAsync(ct);

        // Save alternative responsibles
        await SaveAlternativeResponsiblesAsync(context, entity.EractivityId, command.AlternativeResponsibleUserIds, ct);
        await context.SaveChangesAsync(ct);

        return entity.EractivityId;
    }

    /// <summary>
    /// Updates an existing activity with the given command.
    /// </summary>
    public async Task UpdateActivityAsync(int activityId, ActivitySaveCommand command, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        context.CurrentSiteId = _sessionContext.SiteId;
        context.CurrentClientId = command.ClientId;

        var entity = await context.Eractivities
            .Where(a => a.EractivityId == activityId)
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException($"Activity {activityId} not found.");

        MapCommandToEntity(command, entity);
        entity.EditedId = Guid.NewGuid();
        entity.StatusChangedTimeStamp = DateTime.Now;

        await SaveAlternativeResponsiblesAsync(context, activityId, command.AlternativeResponsibleUserIds, ct);
        await context.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Soft-deletes an activity (sets status to Deleted = 3).
    /// </summary>
    public async Task DeleteActivityAsync(int activityId, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        context.CurrentSiteId = _sessionContext.SiteId;

        var entity = await context.Eractivities
            .Where(a => a.EractivityId == activityId)
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException($"Activity {activityId} not found.");

        entity.EractivityStatusId = 3; // Deleted
        entity.StatusChangedTimeStamp = DateTime.Now;
        entity.EditedId = Guid.NewGuid();
        await context.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Closes an activity (sets status to Closed = 2).
    /// </summary>
    public async Task CloseActivityAsync(int activityId, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        context.CurrentSiteId = _sessionContext.SiteId;

        var entity = await context.Eractivities
            .Where(a => a.EractivityId == activityId)
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException($"Activity {activityId} not found.");

        entity.EractivityStatusId = 2; // Closed
        entity.StatusChangedTimeStamp = DateTime.Now;
        entity.EditedId = Guid.NewGuid();
        await context.SaveChangesAsync(ct);
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private static Data.Entities.Eractivity MapCommandToEntity(
        ActivitySaveCommand command,
        Data.Entities.Eractivity entity)
    {
        entity.ClientId = command.ClientId;
        entity.EractivityStatusId = command.StatusId;
        entity.Headline = command.Headline;
        entity.Jobtitle = command.JobTitle ?? "";
        entity.JobnetOccupationId = command.JobnetOccupationId;
        entity.ClientSectionId = command.ClientSectionId;
        entity.IsReposting = command.Reposting;
        entity.IsLeadershipPosition = command.LeadershipPositionId.HasValue
            ? command.LeadershipPositionId == 1
            : null;
        entity.IsBlindRecruitment = command.BlindRecruitmentId.HasValue
            ? command.BlindRecruitmentId == 1
            : null;
        entity.LockCandidateEvalutionAndNotesWhenUserHasNotEvaluated = command.LockCandidateEvaluation;
        entity.ViewRecruitmentCommitteeEvaluations = command.ViewCommitteeEvaluations;
        entity.EmailOnNewCandidate = command.SendEmailOnNewCandidate;
        entity.SendDailyStatusEmailEnabled = command.SendDailyStatusMail;
        entity.ContinuousPosting = command.ContinuousPosting;
        entity.ApplicationDeadline = command.ApplicationDeadline ?? DateTime.MaxValue;
        entity.HireDate = command.HireDateType == 1 ? command.HireDate : null;
        entity.HireDateFreeText = command.HireDateType == 2 ? command.HireDateFreeText : null;
        entity.InterviewRounds = command.InterviewRounds ?? 1;
        entity.CalendarTypeId = command.CalendarTypeId ?? 0;
        entity.InterviewDuration = command.InterviewDurationId;
        entity.CandidateExtendedEvaluationEnabled = command.ExtendedEvaluationEnabled;
        entity.SendSmsInterviewRemembrances = command.SendSmsInterviewReminders;
        entity.ErtemplateGroupId = command.TemplateGroupId;
        entity.ErapplicationTemplateId = command.ApplicationTemplateId ?? 0;
        entity.ErletterTemplateReceivedId = command.EmailTemplateReceivedId ?? 0;
        entity.ErletterTemplateInterviewId = command.EmailTemplateInterviewId ?? 0;
        entity.ErletterTemplateInterviewTwoPlusRoundsId = command.EmailTemplateInterview2PlusId;
        entity.ErletterTemplateRejectedId = command.EmailTemplateRejectedId ?? 0;
        entity.ErletterTemplateRejectedAfterInterviewId = command.EmailTemplateRejectedAfterInterviewId ?? 0;
        entity.ErnotifyRecruitmentCommitteeId = command.EmailTemplateNotifyCommitteeId ?? 0;
        entity.ErsmsTemplateInterviewId = command.SmsTemplateInterviewId;
        entity.Responsible = command.RecruitmentResponsibleUserId;
        return entity;
    }

    private static async Task SaveAlternativeResponsiblesAsync(
        Data.SignaturDbContext context,
        int activityId,
        List<Guid> userIds,
        CancellationToken ct)
    {
        // Remove existing
        var existing = await context.EractivityAlternativeResponsibles
            .Where(ar => ar.EractivityId == activityId)
            .ToListAsync(ct);
        context.EractivityAlternativeResponsibles.RemoveRange(existing);

        // Add new
        foreach (var userId in userIds)
        {
            context.EractivityAlternativeResponsibles.Add(new Data.Entities.EractivityAlternativeResponsible
            {
                EractivityId = activityId,
                UserId = userId
            });
        }
    }

}
