using SignaturPortal.Application.DTOs;

namespace SignaturPortal.Application.Interfaces;

/// <summary>
/// Service for client-related operations.
/// Provides client lookup data for dropdown selectors.
/// </summary>
public interface IClientService
{
    /// <summary>
    /// Gets recruitment-enabled clients for a site (ERecruitmentEnabled=1), including disabled ones (IsEnabled=false).
    /// Disabled clients are included so the UI can display them as [ClientName], matching legacy bracket notation.
    /// Used to populate the client selector dropdown for non-client (staff/admin) users.
    /// </summary>
    Task<List<ClientDropdownDto>> GetClientsForSiteAsync(int siteId, CancellationToken ct = default);

    /// <summary>
    /// Gets clients for a site that have ERecruitmentEnabled=1 and RecruitmentDraftEnabled, including disabled ones (IsEnabled=false).
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

    /// <summary>
    /// Returns true if the client's default hosting application type is FundApplication.
    /// Used to switch "Recruiting responsable" label to "Grant manager" (RecruitingResponsableFund).
    /// Reads from Sig_Client.CustomData XPath: /ClientCustomData/DefaultHosting/@GeneralDefaultHostingApplicationType.
    /// Matches legacy ClientHlp.ClientDefaultHostingApplicationType comparison.
    /// </summary>
    Task<bool> GetRecruitmentIsFundAsync(int clientId, CancellationToken ct = default);

    /// <summary>
    /// Returns draft area column settings for the given client.
    /// Reads UserResponsabilityAreaTypeId and ListAreaHeaderTextId from DraftSettings XML,
    /// and ClientSection hierarchy flag from ClientSection XML.
    /// Used to control the Draft Area column in the activity list.
    /// Matches legacy ClientHlp.ClientRecruitmentDraftSettings + ClientSectionHierachyEnabled.
    /// </summary>
    Task<RecruitmentDraftSettingsDto> GetRecruitmentDraftSettingsAsync(int clientId, CancellationToken ct = default);

    /// <summary>
    /// Returns true if the client has ERecruitment enabled and ShowWebAdStatusInActivityList enabled.
    /// Maps to Sig_Client.ERecruitmentEnabled and Sig_Client.ERecruitmentShowWebAdStatusInActivityList columns.
    /// Matches legacy ClientHlp.ClientRecruitmentShowWebAdStatusInActivityList.
    /// </summary>
    Task<bool> GetRecruitmentShowWebAdStatusInActivityListAsync(int clientId, CancellationToken ct = default);

    /// <summary>
    /// Returns true if the client has WebAd enabled and WebAdSendMailOnAdChanges enabled.
    /// Reads from Sig_Client.CustomData XPath: /ClientCustomData/AdPortal/@WebAdSendMailOnAdChanges.
    /// Matches legacy ClientHlp.ClientWebAdSendMailOnAdChanges.
    /// </summary>
    Task<bool> GetWebAdSendMailOnAdChangesAsync(int clientId, CancellationToken ct = default);
}
