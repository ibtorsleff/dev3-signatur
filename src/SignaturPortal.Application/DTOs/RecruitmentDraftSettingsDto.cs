namespace SignaturPortal.Application.DTOs;

/// <summary>
/// Client-level draft feature settings needed to control the Draft Area column in the activity list.
/// Mirrors legacy RecruitmentDraftSettings fields read from Sig_Client.CustomData XML:
///   /ClientCustomData/Recruitment/DraftSettings/@UserResponsabilityAreaTypeId
///   /ClientCustomData/Recruitment/DraftSettings/@ListAreaHeaderTextId
///   /ClientCustomData/ClientSection/@HierachyEnabled
/// </summary>
public record RecruitmentDraftSettingsDto
{
    /// <summary>
    /// 0=Undefined, 1=ClientSection, 2=ERTemplateGroup.
    /// Matches legacy RecruitmentDraftSettings.UserResponsabilityAreaTypeEn.
    /// </summary>
    public int UserResponsabilityAreaTypeId { get; init; }

    /// <summary>
    /// Localization key for the Draft Area column header.
    /// Defaults to "ERDraftListAreaHeader" when not configured.
    /// </summary>
    public string ListAreaHeaderTextId { get; init; } = "ERDraftListAreaHeader";

    /// <summary>
    /// Localization key for the Draft Responsible column header.
    /// Defaults to "ERDraftResponsibleHeader" when not configured.
    /// Allows per-client override — e.g. client 2322 uses "ERDraftResponsibleHeader2322" → "Superbruger".
    /// </summary>
    public string DraftResponsibleHeaderTextId { get; init; } = "ERDraftResponsibleHeader";

    /// <summary>
    /// True when the client has ClientSection hierarchy enabled.
    /// Required for showing the Draft Area column when AreaType == ClientSection.
    /// Matches legacy ClientHlp.ClientSectionHierachyEnabled.
    /// </summary>
    public bool SectionHierarchyEnabled { get; init; }
}
