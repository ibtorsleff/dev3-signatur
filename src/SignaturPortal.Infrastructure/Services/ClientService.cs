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
    /// Gets enabled clients for a site with names extracted from ObjectData XML via SQL XPath.
    /// Does not set CurrentSiteId/CurrentClientId -- raw SQL bypasses EF global filters.
    /// </summary>
    public async Task<List<ClientDropdownDto>> GetClientsForSiteAsync(int siteId, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        return await context.Database
            .SqlQueryRaw<ClientDropdownDto>(
                @"SELECT c.ClientId,
                         c.ObjectData.value('(/Client/ClientName)[1]','NVARCHAR(128)') AS ClientName
                  FROM Client c
                  WHERE c.SiteId = {0}
                    AND c.ObjectData.value('(/Client/Enabled)[1]','NVARCHAR(5)') = 'true'
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
                         c.ObjectData.value('(/Client/ClientName)[1]','NVARCHAR(128)') AS ClientName
                  FROM Client c
                  JOIN Sig_Client sc ON sc.ClientId = c.ClientId
                  WHERE c.SiteId = {0}
                    AND c.ObjectData.value('(/Client/Enabled)[1]','NVARCHAR(5)') = 'true'
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

    private class DraftSettingsRow
    {
        public int UserResponsabilityAreaTypeId { get; set; }
        public string? ListAreaHeaderTextId { get; set; }
        public string? DraftResponsibleHeaderTextId { get; set; }
        public bool SectionHierarchyEnabled { get; set; }
    }
}
