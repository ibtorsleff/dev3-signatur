namespace SignaturPortal.Application.DTOs;

/// <summary>
/// Additional filter values from the "More" toolbar panel in the activity list.
/// Applied as server-side WHERE clauses alongside the base GridRequest filters.
/// Only relevant for non-client users (client users do not see the More panel).
/// </summary>
public record ActivityListFilterDto
{
    public Guid? CreatedByUserId { get; init; }
    public Guid? RecruitmentResponsibleUserId { get; init; }
    public int? ClientSectionId { get; init; }
}
