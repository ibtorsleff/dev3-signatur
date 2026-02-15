namespace SignaturPortal.Application.DTOs;

/// <summary>
/// Generic paginated grid response.
/// Contains a page of items and the total count for pagination calculations.
/// </summary>
/// <typeparam name="T">DTO type</typeparam>
public class GridResponse<T>
{
    public List<T> Items { get; set; } = new();
    public int TotalCount { get; set; }
}
