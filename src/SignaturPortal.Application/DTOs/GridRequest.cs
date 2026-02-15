namespace SignaturPortal.Application.DTOs;

/// <summary>
/// Generic paginated grid request with sorting and filtering support.
/// Used for server-side data operations in MudDataGrid and similar components.
/// </summary>
public class GridRequest
{
    public int Page { get; set; }
    public int PageSize { get; set; } = 25;
    public List<SortDefinition> Sorts { get; set; } = new();
    public List<FilterDefinition> Filters { get; set; } = new();
}

/// <summary>
/// Sort definition for a single column.
/// </summary>
/// <param name="PropertyName">Property name to sort by (case-sensitive)</param>
/// <param name="Descending">True for descending sort, false for ascending</param>
public record SortDefinition(string PropertyName, bool Descending);

/// <summary>
/// Filter definition for a single column.
/// </summary>
/// <param name="PropertyName">Property name to filter (case-sensitive)</param>
/// <param name="Operator">Filter operator: "contains", "equals", "startswith", "endswith"</param>
/// <param name="Value">Filter value</param>
public record FilterDefinition(string PropertyName, string Operator, object? Value);
