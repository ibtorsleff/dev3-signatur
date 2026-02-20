namespace SignaturPortal.Application.DTOs;

/// <summary>
/// Per-field visibility, required status, email editing rules, and domain validation
/// lists for the user profile dialog. Computed from user state and client SSO config.
///
/// Business rules migrated from legacy UserProfile.ascx.cs:
///   SetupVarious()        → visibility and required flags
///   UserCanEditEmail      → EmailDisabled / EmailRequired
///   ClientHlp.Client*()  → domain lists for email validation
/// </summary>
public class UserProfileConfigDto
{
    /// <summary>WorkArea is visible only for internal users.</summary>
    public bool ShowWorkArea { get; init; }

    /// <summary>Title is visible only for internal users.</summary>
    public bool ShowTitle { get; init; }

    /// <summary>
    /// Required when: isClientLoggedOn &amp;&amp; isInternal &amp;&amp; clientIsSso
    ///   &amp;&amp; isSsoUser &amp;&amp; SSO.LoginWorkAreaRequired.
    /// </summary>
    public bool WorkAreaRequired { get; init; }

    /// <summary>
    /// Required when: isClientLoggedOn &amp;&amp; isInternal &amp;&amp; clientIsSso
    ///   &amp;&amp; isSsoUser &amp;&amp; SSO.LoginTitelRequired.
    /// </summary>
    public bool TitleRequired { get; init; }

    /// <summary>
    /// Required when: isClientLoggedOn &amp;&amp; clientIsSso &amp;&amp; isSsoUser
    ///   &amp;&amp; SSO.LoginOfficePhoneRequired.
    /// </summary>
    public bool OfficePhoneRequired { get; init; }

    /// <summary>
    /// Required when: (ClientUserCellPhoneRequired &amp;&amp; !isSsoUser)
    ///   OR (clientIsSso &amp;&amp; isSsoUser &amp;&amp; SSO.LoginCellPhoneRequired).
    /// </summary>
    public bool CellPhoneRequired { get; init; }

    /// <summary>True when UserCanEditEmail — email field is editable and required.</summary>
    public bool EmailRequired { get; init; }

    /// <summary>True when !UserCanEditEmail — email field is read-only.</summary>
    public bool EmailDisabled { get; init; }

    /// <summary>
    /// If non-empty and user is internal, the new email domain must be in this list.
    /// Mirrors ClientHlp.ClientUserAllowedEmailDomains() (Sig_Client.CustomData).
    /// </summary>
    public IReadOnlyList<string> AllowedEmailDomains { get; init; } = [];

    /// <summary>
    /// If non-empty, the new email domain must NOT be in this list.
    /// Mirrors ClientHlp.ClientUser[Internal/External]NotAllowedEmailDomains().
    /// </summary>
    public IReadOnlyList<string> BlockedEmailDomains { get; init; } = [];

    /// <summary>
    /// If non-empty and email changes, the new email domain must be in this list.
    /// Mirrors ClientHlp.ClientMailDomains() (client's registered email domains).
    /// </summary>
    public IReadOnlyList<string> ClientMailDomains { get; init; } = [];

    /// <summary>
    /// User must change to a work email domain. Shows a warning and drives email validation.
    /// Mirrors ClientHlp.UserMustChangeEmailToWorkEmail().
    /// NOTE: Full implementation requires Onboarding process data (deferred). Defaults to false.
    /// </summary>
    public bool MustChangeEmailToWorkEmail { get; init; }
}
