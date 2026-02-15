using System.Linq.Dynamic.Core;
using SignaturPortal.Application.DTOs;

namespace SignaturPortal.Infrastructure.Extensions;

/// <summary>
/// Extension methods for IQueryable to support dynamic sorting, filtering, and pagination.
/// Used by service layer for server-side data grid operations.
/// </summary>
public static class QueryableExtensions
{
    /// <summary>
    /// Applies multiple sort operations to a queryable using dynamic LINQ.
    /// If no sorts provided, query is returned unchanged.
    /// </summary>
    public static IQueryable<T> ApplySorts<T>(this IQueryable<T> query, List<SortDefinition> sorts)
    {
        if (sorts == null || sorts.Count == 0)
            return query;

        // Build OrderBy string: "PropertyName desc, OtherProperty asc"
        var orderByParts = sorts.Select(s =>
            s.Descending ? $"{s.PropertyName} descending" : s.PropertyName);
        var orderByString = string.Join(", ", orderByParts);

        return query.OrderBy(orderByString);
    }

    /// <summary>
    /// Applies filter operations to a queryable using dynamic LINQ.
    /// Supports: contains, equals, startswith, endswith operators.
    /// </summary>
    public static IQueryable<T> ApplyFilters<T>(this IQueryable<T> query, List<FilterDefinition> filters)
    {
        if (filters == null || filters.Count == 0)
            return query;

        foreach (var filter in filters)
        {
            if (filter.Value == null)
                continue;

            var propertyName = filter.PropertyName;
            var value = filter.Value;

            switch (filter.Operator.ToLowerInvariant())
            {
                case "contains":
                    query = query.Where($"{propertyName}.Contains(@0)", value);
                    break;
                case "equals":
                    query = query.Where($"{propertyName} == @0", value);
                    break;
                case "startswith":
                    query = query.Where($"{propertyName}.StartsWith(@0)", value);
                    break;
                case "endswith":
                    query = query.Where($"{propertyName}.EndsWith(@0)", value);
                    break;
                default:
                    // Ignore unknown operators
                    break;
            }
        }

        return query;
    }

    /// <summary>
    /// Applies pagination to a queryable using Skip/Take.
    /// Page is zero-based (0 = first page).
    /// </summary>
    public static IQueryable<T> ApplyPage<T>(this IQueryable<T> query, int page, int pageSize)
    {
        if (page < 0) page = 0;
        if (pageSize <= 0) pageSize = 25;

        return query
            .Skip(page * pageSize)
            .Take(pageSize);
    }
}
