namespace SignaturPortal.Application.DTOs;

/// <summary>
/// Dropdown options for the "More" filter panel in the activity list.
/// Lists are derived from distinct values found in existing activities
/// for the current client and activity status context.
/// </summary>
public record ActivityFilterOptionsDto
{
    public List<UserDropdownDto> CreatedByUsers { get; init; } = new();
    public List<UserDropdownDto> RecruitmentResponsibleUsers { get; init; } = new();
    public List<ClientSectionDropdownDto> ClientSections { get; init; } = new();
}

/// <summary>
/// A single user entry for "Created By" or "Recruitment Responsible" filter dropdowns.
/// </summary>
public record UserDropdownDto
{
    public Guid UserId { get; init; }
    public string DisplayName { get; init; } = "";
}

/// <summary>
/// A single client section entry for the "Client Section" filter dropdown.
/// </summary>
public record ClientSectionDropdownDto
{
    public int ClientSectionId { get; init; }
    public string Name { get; init; } = "";
}
