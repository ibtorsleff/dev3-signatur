namespace SignaturPortal.Application.DTOs;

/// <summary>
/// Activity detail DTO for detail view.
/// TODO: Complete in Plan 03-03 with all detail fields.
/// </summary>
public record ActivityDetailDto
{
    public int EractivityId { get; init; }
    public string Headline { get; init; } = "";
    // TODO: Add remaining detail fields in Plan 03-03
}
