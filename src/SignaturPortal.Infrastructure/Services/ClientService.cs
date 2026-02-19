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
}
