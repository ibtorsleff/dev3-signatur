using SignaturPortal.Application.DTOs;

namespace SignaturPortal.Application.Interfaces;

/// <summary>
/// Service for client-related operations.
/// Provides client lookup data for dropdown selectors.
/// </summary>
public interface IClientService
{
    /// <summary>
    /// Gets a list of enabled clients for a given site, with names extracted from ObjectData XML.
    /// Used to populate the client selector dropdown for non-client (staff/admin) users.
    /// </summary>
    Task<List<ClientDropdownDto>> GetClientsForSiteAsync(int siteId, CancellationToken ct = default);

    /// <summary>
    /// Gets enabled clients for a site that also have RecruitmentDraftEnabled.
    /// Used in Draft mode — matches legacy ClientsGet with filters RecruitmentEnabled + RecruitmentDraftEnabled.
    /// </summary>
    Task<List<ClientDropdownDto>> GetClientsForSiteWithDraftEnabledAsync(int siteId, CancellationToken ct = default);

    /// <summary>
    /// Returns true if the client has WebAd enabled and WebAdVisitorStatistics enabled.
    /// Maps to Sig_Client.WebAdEnabled and Sig_Client.WebAdVisitorStatisticsEnabled columns.
    /// </summary>
    Task<bool> GetWebAdVisitorStatisticsEnabledAsync(int clientId, CancellationToken ct = default);

    /// <summary>
    /// Returns true if the client has ERecruitment enabled.
    /// Maps to Sig_Client.ERecruitmentEnabled column.
    /// Matches legacy ClientHlp.ClientRecruitmentEnabled.
    /// </summary>
    Task<bool> GetRecruitmentEnabledAsync(int clientId, CancellationToken ct = default);

    /// <summary>
    /// Returns true if the client has ERecruitment enabled and uses template groups.
    /// Maps to Sig_Client.ERecruitmentEnabled and Sig_Client.ERecruitmentUseTemplateGroups columns.
    /// </summary>
    Task<bool> GetRecruitmentUseTemplateGroupsAsync(int clientId, CancellationToken ct = default);

    /// <summary>
    /// Returns true if the client allows exporting activity members from the activity list.
    /// Reads from Sig_Client.CustomData XPath: /ClientCustomData/Recruitment/@ExportActivityMembersInActivityListEnabled.
    /// For internal (non-client) users this is always true — only checked for client users.
    /// </summary>
    Task<bool> GetExportActivityMembersEnabledAsync(int clientId, CancellationToken ct = default);

    /// <summary>
    /// Returns true if the client has ERecruitment enabled and the Recruitment Draft feature enabled.
    /// Reads from Sig_Client.CustomData XPath: /ClientCustomData/Recruitment/DraftSettings/@DraftEnabled.
    /// Matches legacy ClientHlp.ClientRecruitmentDraftEnabled.
    /// </summary>
    Task<bool> GetRecruitmentDraftEnabledAsync(int clientId, CancellationToken ct = default);
}
