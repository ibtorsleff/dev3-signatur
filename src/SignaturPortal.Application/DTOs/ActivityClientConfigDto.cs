namespace SignaturPortal.Application.DTOs;

/// <summary>
/// Client-specific feature flags that control field visibility and requirements
/// on the ActivityCreateEdit form.
/// Loaded when a client is selected; reloaded on client change.
/// </summary>
public record ActivityClientConfigDto
{
    /// <summary>Client uses template groups (ERecruitmentUseTemplateGroups).</summary>
    public bool UseTemplateGroups { get; init; }

    /// <summary>Client uses client section groups (ClientSectionHierarchyEnabled).</summary>
    public bool UseClientSectionGroups { get; init; }

    /// <summary>Multiple interview rounds are enabled for this client.</summary>
    public bool MultipleInterviewRoundsEnabled { get; init; }

    /// <summary>Calendar scheduling (open/closed) is enabled for this client.</summary>
    public bool CalendarEnabled { get; init; }

    /// <summary>Client allows sending SMS reminders.</summary>
    public bool AllowSendingSms { get; init; }

    /// <summary>Client has daily status mail feature enabled.</summary>
    public bool SendDailyStatusMailEnabled { get; init; }

    /// <summary>Client has continuous posting feature enabled.</summary>
    public bool ContinuousPostingEnabled { get; init; }

    /// <summary>Client supports multiple languages on an activity.</summary>
    public bool MultipleLanguagesEnabled { get; init; }

    /// <summary>
    /// Basic candidate evaluation is available for this client.
    /// Maps to Sig_Client.ERecruitmentCandidateEvaluationEnabled.
    /// Required prerequisite for ExtendedEvaluationEnabled visibility.
    /// </summary>
    public bool CandidateEvaluationEnabled { get; init; }

    /// <summary>
    /// Extended candidate evaluation is available for this client.
    /// Maps to Sig_Client.ERecruitmentCandidateExtendedEvaluationEnabled.
    /// Only shown when CandidateEvaluationEnabled is also true.
    /// </summary>
    public bool ExtendedEvaluationEnabled { get; init; }

    /// <summary>Lock candidate evaluation feature is available.</summary>
    public bool LockEvaluationFeatureEnabled { get; init; }

    /// <summary>Client is a fund client (changes some labels/UI).</summary>
    public bool IsFundClient { get; init; }

    /// <summary>Leadership position marking on activity is enabled for this client.</summary>
    public bool LeadershipPositionEnabled { get; init; }

    /// <summary>Blind recruitment is enabled for this client.</summary>
    public bool BlindRecruitmentEnabled { get; init; }

    /// <summary>
    /// Limited candidate access (leadership position) is enabled.
    /// When both LeadershipPositionEnabled and BlindRecruitmentEnabled are true,
    /// this flag determines whether to show a combined RecruitmentType dropdown (true)
    /// or two separate Yes/No dropdowns (false).
    /// </summary>
    public bool LeadershipPositionLimitedCandidateAccessEnabled { get; init; }
}
