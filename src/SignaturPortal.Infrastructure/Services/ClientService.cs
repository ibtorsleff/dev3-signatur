using Microsoft.EntityFrameworkCore;
using SignaturPortal.Application.DTOs;
using SignaturPortal.Application.Interfaces;
using SignaturPortal.Infrastructure.Data;

namespace SignaturPortal.Infrastructure.Services;

/// <summary>
/// Service for client-related operations.
/// Uses raw SQL with XPath to extract client names from the ObjectData XML column,
/// matching the legacy pattern exactly (no C# XML parsing).
/// </summary>
public class ClientService : IClientService
{
    private readonly IDbContextFactory<SignaturDbContext> _contextFactory;

    public ClientService(IDbContextFactory<SignaturDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    /// <summary>
    /// Gets recruitment-enabled clients for a site, including disabled ones (marked IsEnabled=false).
    /// Joins Sig_Client to filter by ERecruitmentEnabled=1, matching legacy ClientListFilter.RecruitmentEnabled.
    /// Disabled clients (Client.ObjectData/Enabled='false') are included so the UI can display them as [ClientName].
    /// Does not set CurrentSiteId/CurrentClientId -- raw SQL bypasses EF global filters.
    /// </summary>
    public async Task<List<ClientDropdownDto>> GetClientsForSiteAsync(int siteId, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        return await context.Database
            .SqlQueryRaw<ClientDropdownDto>(
                @"SELECT c.ClientId,
                         c.ObjectData.value('(/Client/ClientName)[1]','NVARCHAR(128)') AS ClientName,
                         CAST(CASE WHEN c.ObjectData.value('(/Client/Enabled)[1]','NVARCHAR(5)') = 'true' THEN 1 ELSE 0 END AS BIT) AS IsEnabled
                  FROM Client c
                  JOIN Sig_Client sc ON sc.ClientId = c.ClientId
                  WHERE c.SiteId = {0}
                    AND sc.ERecruitmentEnabled = 1
                  ORDER BY c.ObjectData.value('(/Client/ClientName)[1]','NVARCHAR(128)')",
                siteId)
            .ToListAsync(ct);
    }

    public async Task<List<ClientDropdownDto>> GetClientsForSiteWithDraftEnabledAsync(int siteId, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        return await context.Database
            .SqlQueryRaw<ClientDropdownDto>(
                @"SELECT c.ClientId,
                         c.ObjectData.value('(/Client/ClientName)[1]','NVARCHAR(128)') AS ClientName,
                         CAST(CASE WHEN c.ObjectData.value('(/Client/Enabled)[1]','NVARCHAR(5)') = 'true' THEN 1 ELSE 0 END AS BIT) AS IsEnabled
                  FROM Client c
                  JOIN Sig_Client sc ON sc.ClientId = c.ClientId
                  WHERE c.SiteId = {0}
                    AND sc.ERecruitmentEnabled = 1
                    AND sc.CustomData.value('(/ClientCustomData/Recruitment/DraftSettings/@DraftEnabled)[1]','NVARCHAR(5)') = 'true'
                  ORDER BY c.ObjectData.value('(/Client/ClientName)[1]','NVARCHAR(128)')",
                siteId)
            .ToListAsync(ct);
    }

    public async Task<bool> GetWebAdVisitorStatisticsEnabledAsync(int clientId, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var result = await context.Database
            .SqlQueryRaw<bool>(
                @"SELECT CAST(SC.WebAdVisitorStatisticsEnabled AS BIT)
                  FROM Sig_Client SC
                  WHERE SC.ClientId = {0}
                    AND SC.WebAdEnabled = 1",
                clientId)
            .ToListAsync(ct);
        return result.FirstOrDefault();
    }

    public async Task<bool> GetRecruitmentEnabledAsync(int clientId, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var result = await context.Database
            .SqlQueryRaw<bool>(
                @"SELECT CAST(SC.ERecruitmentEnabled AS BIT)
                  FROM Sig_Client SC
                  WHERE SC.ClientId = {0}",
                clientId)
            .ToListAsync(ct);
        return result.FirstOrDefault();
    }

    public async Task<bool> GetRecruitmentUseTemplateGroupsAsync(int clientId, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var result = await context.Database
            .SqlQueryRaw<bool>(
                @"SELECT CAST(SC.ERecruitmentUseTemplateGroups AS BIT)
                  FROM Sig_Client SC
                  WHERE SC.ClientId = {0}
                    AND SC.ERecruitmentEnabled = 1",
                clientId)
            .ToListAsync(ct);
        return result.FirstOrDefault();
    }

    public async Task<bool> GetExportActivityMembersEnabledAsync(int clientId, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var result = await context.Database
            .SqlQueryRaw<bool>(
                @"SELECT CAST(1 AS BIT)
                  FROM Sig_Client SCLI
                  WHERE SCLI.ClientId = {0}
                    AND SCLI.ERecruitmentEnabled = 1
                    AND SCLI.CustomData.value('(/ClientCustomData/Recruitment/@ExportActivityMembersInActivityListEnabled)[1]','NVARCHAR(5)') = 'true'",
                clientId)
            .ToListAsync(ct);
        return result.FirstOrDefault();
    }

    public async Task<bool> GetRecruitmentDraftEnabledAsync(int clientId, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var result = await context.Database
            .SqlQueryRaw<bool>(
                @"SELECT CAST(1 AS BIT)
                  FROM Sig_Client SCLI
                  WHERE SCLI.ClientId = {0}
                    AND SCLI.ERecruitmentEnabled = 1
                    AND SCLI.CustomData.value('(/ClientCustomData/Recruitment/DraftSettings/@DraftEnabled)[1]','NVARCHAR(5)') = 'true'",
                clientId)
            .ToListAsync(ct);
        return result.FirstOrDefault();
    }

    public async Task<bool> GetRecruitmentIsFundAsync(int clientId, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var result = await context.Database
            .SqlQueryRaw<bool>(
                @"SELECT CAST(1 AS BIT)
                  FROM Sig_Client SCLI
                  WHERE SCLI.ClientId = {0}
                    AND SCLI.CustomData.value('(/ClientCustomData/DefaultHosting/@GeneralDefaultHostingApplicationType)[1]','NVARCHAR(20)') = 'FundApplication'",
                clientId)
            .ToListAsync(ct);
        return result.FirstOrDefault();
    }

    public async Task<RecruitmentDraftSettingsDto> GetRecruitmentDraftSettingsAsync(int clientId, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var rows = await context.Database
            .SqlQueryRaw<DraftSettingsRow>(
                @"SELECT
                    ISNULL(SCLI.CustomData.value('(/ClientCustomData/Recruitment/DraftSettings/@UserResponsabilityAreaTypeId)[1]', 'INT'), 0) AS UserResponsabilityAreaTypeId,
                    ISNULL(SCLI.CustomData.value('(/ClientCustomData/Recruitment/DraftSettings/@ListAreaHeaderTextId)[1]', 'NVARCHAR(200)'), '') AS ListAreaHeaderTextId,
                    ISNULL(SCLI.CustomData.value('(/ClientCustomData/Recruitment/DraftSettings/@DraftResponsibleHeaderTextId)[1]', 'NVARCHAR(200)'), '') AS DraftResponsibleHeaderTextId,
                    CAST(CASE WHEN SCLI.CustomData.value('(/ClientCustomData/ClientSection/@HierachyEnabled)[1]', 'NVARCHAR(5)') = 'true' THEN 1 ELSE 0 END AS BIT) AS SectionHierarchyEnabled
                  FROM Sig_Client SCLI
                  WHERE SCLI.ClientId = {0}",
                clientId)
            .ToListAsync(ct);

        var row = rows.FirstOrDefault();
        return new RecruitmentDraftSettingsDto
        {
            UserResponsabilityAreaTypeId = row?.UserResponsabilityAreaTypeId ?? 0,
            ListAreaHeaderTextId = string.IsNullOrEmpty(row?.ListAreaHeaderTextId)
                ? "ERDraftListAreaHeader"
                : row.ListAreaHeaderTextId,
            DraftResponsibleHeaderTextId = string.IsNullOrEmpty(row?.DraftResponsibleHeaderTextId)
                ? "ERDraftResponsibleHeader"
                : row.DraftResponsibleHeaderTextId,
            SectionHierarchyEnabled = row?.SectionHierarchyEnabled ?? false
        };
    }

    public async Task<bool> GetRecruitmentShowWebAdStatusInActivityListAsync(int clientId, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var result = await context.Database
            .SqlQueryRaw<bool>(
                @"SELECT CAST(SC.ERecruitmentShowWebAdStatusInActivityList AS BIT)
                  FROM Sig_Client SC
                  WHERE SC.ClientId = {0}
                    AND SC.ERecruitmentEnabled = 1",
                clientId)
            .ToListAsync(ct);
        return result.FirstOrDefault();
    }

    public async Task<bool> GetWebAdSendMailOnAdChangesAsync(int clientId, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var result = await context.Database
            .SqlQueryRaw<bool>(
                @"SELECT CAST(1 AS BIT)
                  FROM Sig_Client SCLI
                  WHERE SCLI.ClientId = {0}
                    AND SCLI.WebAdEnabled = 1
                    AND SCLI.CustomData.value('(/ClientCustomData/AdPortal/@WebAdSendMailOnAdChanges)[1]','NVARCHAR(5)') = 'true'",
                clientId)
            .ToListAsync(ct);
        return result.FirstOrDefault();
    }

    /// <summary>
    /// Returns client-specific feature flags that control field visibility on the ActivityCreateEdit form.
    /// Reads from Sig_Client columns and CustomData XML.
    /// </summary>
    public async Task<ActivityClientConfigDto> GetActivityClientConfigAsync(int clientId, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var result = await context.Database
            .SqlQueryRaw<ActivityClientConfigRow>(
                @"SELECT
                    CAST(ISNULL(sc.ERecruitmentUseTemplateGroups, 0) AS BIT) AS UseTemplateGroups,
                    CAST(ISNULL(sc.CustomData.value('(/ClientCustomData/ClientSection/@SectionHierarchyEnabled)[1]','NVARCHAR(5)'), 'false') AS NVARCHAR(5)) AS UseClientSectionGroupsRaw,
                    CAST(ISNULL(sc.CustomData.value('(/ClientCustomData/Recruitment/@MultipleInterviewRoundsEnabled)[1]','NVARCHAR(5)'), 'false') AS NVARCHAR(5)) AS MultipleInterviewRoundsEnabledRaw,
                    CAST(ISNULL(sc.CustomData.value('(/ClientCustomData/Recruitment/@CalendarEnabled)[1]','NVARCHAR(5)'), 'false') AS NVARCHAR(5)) AS CalendarEnabledRaw,
                    CAST(ISNULL(sc.CustomData.value('(/ClientCustomData/Recruitment/@AllowSendingSms)[1]','NVARCHAR(5)'), 'false') AS NVARCHAR(5)) AS AllowSendingSmsRaw,
                    CAST(ISNULL(sc.CustomData.value('(/ClientCustomData/Recruitment/@SendDailyStatusMailEnabled)[1]','NVARCHAR(5)'), 'false') AS NVARCHAR(5)) AS SendDailyStatusMailEnabledRaw,
                    CAST(ISNULL(sc.CustomData.value('(/ClientCustomData/Recruitment/@ContinuousPostingEnabled)[1]','NVARCHAR(5)'), 'false') AS NVARCHAR(5)) AS ContinuousPostingEnabledRaw,
                    CAST(ISNULL(sc.CustomData.value('(/ClientCustomData/Recruitment/@MultipleLanguagesEnabled)[1]','NVARCHAR(5)'), 'false') AS NVARCHAR(5)) AS MultipleLanguagesEnabledRaw,
                    CAST(ISNULL(sc.CustomData.value('(/ClientCustomData/Recruitment/@ExtendedEvaluationEnabled)[1]','NVARCHAR(5)'), 'false') AS NVARCHAR(5)) AS ExtendedEvaluationEnabledRaw,
                    CAST(ISNULL(sc.CustomData.value('(/ClientCustomData/Recruitment/@LockEvaluationEnabled)[1]','NVARCHAR(5)'), 'false') AS NVARCHAR(5)) AS LockEvaluationFeatureEnabledRaw,
                    CAST(ISNULL(sc.CustomData.value('(/ClientCustomData/DefaultHosting/@GeneralDefaultHostingApplicationType)[1]','NVARCHAR(50)'), '') AS NVARCHAR(50)) AS DefaultHostingTypeRaw,
                    CAST(ISNULL(sc.CustomData.value('(/ClientCustomData/Recruitment/@LeadershipPositionMarkOnActivityEnabled)[1]','NVARCHAR(5)'), 'false') AS NVARCHAR(5)) AS LeadershipPositionEnabledRaw,
                    CAST(ISNULL(sc.CustomData.value('(/ClientCustomData/Recruitment/@BlindRecruitmentEnabled)[1]','NVARCHAR(5)'), 'false') AS NVARCHAR(5)) AS BlindRecruitmentEnabledRaw,
                    CAST(ISNULL(sc.CustomData.value('(/ClientCustomData/Recruitment/@LeadershipPositionLimitedCandidateAccessEnabled)[1]','NVARCHAR(5)'), 'false') AS NVARCHAR(5)) AS LimitedCandidateAccessEnabledRaw
                  FROM Sig_Client sc
                  WHERE sc.ClientId = {0}",
                clientId)
            .ToListAsync(ct);

        if (!result.Any())
        {
            return new ActivityClientConfigDto();
        }

        var row = result.First();
        return new ActivityClientConfigDto
        {
            UseTemplateGroups = row.UseTemplateGroups,
            UseClientSectionGroups = string.Equals(row.UseClientSectionGroupsRaw, "true", StringComparison.OrdinalIgnoreCase),
            MultipleInterviewRoundsEnabled = string.Equals(row.MultipleInterviewRoundsEnabledRaw, "true", StringComparison.OrdinalIgnoreCase),
            CalendarEnabled = string.Equals(row.CalendarEnabledRaw, "true", StringComparison.OrdinalIgnoreCase),
            AllowSendingSms = string.Equals(row.AllowSendingSmsRaw, "true", StringComparison.OrdinalIgnoreCase),
            SendDailyStatusMailEnabled = string.Equals(row.SendDailyStatusMailEnabledRaw, "true", StringComparison.OrdinalIgnoreCase),
            ContinuousPostingEnabled = string.Equals(row.ContinuousPostingEnabledRaw, "true", StringComparison.OrdinalIgnoreCase),
            MultipleLanguagesEnabled = string.Equals(row.MultipleLanguagesEnabledRaw, "true", StringComparison.OrdinalIgnoreCase),
            ExtendedEvaluationEnabled = string.Equals(row.ExtendedEvaluationEnabledRaw, "true", StringComparison.OrdinalIgnoreCase),
            LockEvaluationFeatureEnabled = string.Equals(row.LockEvaluationFeatureEnabledRaw, "true", StringComparison.OrdinalIgnoreCase),
            IsFundClient = string.Equals(row.DefaultHostingTypeRaw, "FundApplication", StringComparison.OrdinalIgnoreCase),
            LeadershipPositionEnabled = string.Equals(row.LeadershipPositionEnabledRaw, "true", StringComparison.OrdinalIgnoreCase),
            BlindRecruitmentEnabled = string.Equals(row.BlindRecruitmentEnabledRaw, "true", StringComparison.OrdinalIgnoreCase),
            LeadershipPositionLimitedCandidateAccessEnabled = string.Equals(row.LimitedCandidateAccessEnabledRaw, "true", StringComparison.OrdinalIgnoreCase)
        };
    }

    private class DraftSettingsRow
    {
        public int UserResponsabilityAreaTypeId { get; set; }
        public string? ListAreaHeaderTextId { get; set; }
        public string? DraftResponsibleHeaderTextId { get; set; }
        public bool SectionHierarchyEnabled { get; set; }
    }

    private class ActivityClientConfigRow
    {
        public bool UseTemplateGroups { get; set; }
        public string? UseClientSectionGroupsRaw { get; set; }
        public string? MultipleInterviewRoundsEnabledRaw { get; set; }
        public string? CalendarEnabledRaw { get; set; }
        public string? AllowSendingSmsRaw { get; set; }
        public string? SendDailyStatusMailEnabledRaw { get; set; }
        public string? ContinuousPostingEnabledRaw { get; set; }
        public string? MultipleLanguagesEnabledRaw { get; set; }
        public string? ExtendedEvaluationEnabledRaw { get; set; }
        public string? LockEvaluationFeatureEnabledRaw { get; set; }
        public string? DefaultHostingTypeRaw { get; set; }
        public string? LeadershipPositionEnabledRaw { get; set; }
        public string? BlindRecruitmentEnabledRaw { get; set; }
        public string? LimitedCandidateAccessEnabledRaw { get; set; }
    }
}
