namespace SignaturPortal.Application.DTOs;

/// <summary>
/// Bundles all dropdown/option lists needed to render the ActivityCreateEdit form.
/// Loaded once per client selection; reloaded on client change.
/// </summary>
public record ActivityFormOptionsDto
{
    public List<ActivityStatusOptionDto> Statuses { get; init; } = new();
    public List<SimpleOptionDto> JobnetOccupations { get; init; } = new();
    public List<ClientSectionGroupDropdownDto> ClientSectionGroups { get; init; } = new();
    // ClientSections are loaded via live-search (GetClientSectionsForFormAsync)
    public List<SimpleOptionDto> LeadershipPositions { get; init; } = new();
    public List<SimpleOptionDto> BlindRecruitmentOptions { get; init; } = new();
    public List<SimpleOptionDto> RecruitmentTypes { get; init; } = new();
    public List<SimpleOptionDto> InterviewRoundsOptions { get; init; } = new();
    public List<SimpleOptionDto> CalendarTypes { get; init; } = new();
    public List<SimpleOptionDto> InterviewDurations { get; init; } = new();
    public List<SimpleOptionDto> Languages { get; init; } = new();
    public List<TemplateGroupDropdownDto> TemplateGroups { get; init; } = new();
    public List<SimpleOptionDto> ApplicationTemplates { get; init; } = new();
    public List<SimpleOptionDto> EmailTemplatesReceived { get; init; } = new();
    public List<SimpleOptionDto> EmailTemplatesInterview { get; init; } = new();
    public List<SimpleOptionDto> EmailTemplatesInterview2Plus { get; init; } = new();
    public List<SimpleOptionDto> EmailTemplatesRejected { get; init; } = new();
    public List<SimpleOptionDto> EmailTemplatesRejectedAfterInterview { get; init; } = new();
    public List<SimpleOptionDto> EmailTemplatesNotifyCommittee { get; init; } = new();
    public List<SimpleOptionDto> SmsTemplates { get; init; } = new();

    // Sidebar pre-loaded for edit mode
    public UserDropdownDto? CurrentResponsible { get; init; }
    public List<UserDropdownDto> AlternativeResponsibles { get; init; } = new();
    public List<HiringTeamMemberDto> CommitteeMembers { get; init; } = new();
}

/// <summary>
/// Generic id/name option for simple select dropdowns.
/// </summary>
public record SimpleOptionDto(int Id, string Name);

/// <summary>
/// Activity status option with localized display name.
/// </summary>
public record ActivityStatusOptionDto(int Id, string Name);
