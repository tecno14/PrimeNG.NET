using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PrimeNG.NET.Requests;
using System.Linq.Dynamic.Core;
using System.Reflection;

namespace PrimeNG.NET.Extensions;

public static class PrimeNgQueryableExtensions
{
    public static IQueryable<T> ApplyPrimeNgFiltering<T>(
        this IQueryable<T> query,
        PrimeNgTableRequest request,
        ILogger? logger = null)
    {
        if (request.Filters == null) 
            return query;

        foreach (var filter in request.Filters)
        {
            try
            {
                var rawValue = filter.Value.Value?.ToString();
                if (string.IsNullOrWhiteSpace(rawValue)) 
                    continue;

                // Case-insensitive property lookup
                var prop = typeof(T).GetProperty(
                    filter.Key, 
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                
                if (prop == null) 
                    continue;

                // 1. Determine the target type (handling nullables)
                Type targetType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;

                // 2. Parse the filter value to match the property type
                object convertedValue;
                try
                {
                    convertedValue = targetType == typeof(Guid)
                        ? Guid.Parse(rawValue)
                        : Convert.ChangeType(rawValue, targetType);
                }
                catch { continue; } // Skip if value can't be converted to prop type

                if (!Enum.TryParse<PrimeNgMatchMode>(filter.Value.MatchMode, true, out var mode))
                    continue;

                // 3. Branching logic based on type
                if (targetType == typeof(string))
                {
                    string val = rawValue.ToLower();
                    query = ApplyStringFilter(query, prop.Name, mode, val);
                }
                else
                {
                    // For Non-strings (Guid, Int, Bool), only Equals/NotEquals make sense
                    query = ApplyExactFilter(query, prop.Name, mode, convertedValue);
                }
            }
            catch (Exception ex)
            {
                logger?.LogWarning("Filter error in {Method}: {Msg}", nameof(ApplyPrimeNgFiltering), ex.Message);
            }
        }
        return query;
    }

    private static IQueryable<T> ApplyStringFilter<T>(
    IQueryable<T> query,
    string propName,
    PrimeNgMatchMode mode,
    string value)
    {
        if (string.IsNullOrEmpty(value))
            return query;

        return mode switch
        {
            PrimeNgMatchMode.Contains =>
                query.Where(x =>
                    x != null &&
                    EF.Functions.Like(EF.Property<string>(x, propName), $"%{value}%")),

            PrimeNgMatchMode.NotContains =>
                query.Where(x =>
                    x != null &&
                    !EF.Functions.Like(EF.Property<string>(x, propName), $"%{value}%")),

            PrimeNgMatchMode.StartsWith =>
                query.Where(x =>
                    x != null &&
                    EF.Functions.Like(EF.Property<string>(x, propName), $"{value}%")),

            PrimeNgMatchMode.EndsWith =>
                query.Where(x =>
                    x != null &&
                    EF.Functions.Like(EF.Property<string>(x, propName), $"%{value}")),

            PrimeNgMatchMode.Equals =>
                query.Where(x =>
                    x != null &&
                    EF.Property<string>(x, propName) == value),

            _ => query
        };
    }

    private static IQueryable<T> ApplyExactFilter<T>(
        IQueryable<T> query,
        string propName,
        PrimeNgMatchMode mode,
        object value)
    {
        if (value is null)
            return query;

        // Use EF.Property<object> and cast it inside the expression or dynamic LINQ
        // For simplicity with multiple types, we check the common modes:
        return mode switch
        {
            PrimeNgMatchMode.Equals =>
                query.Where(x =>
                    x != null &&
                    EF.Property<object>(x, propName) != null &&
                    EF.Property<object>(x, propName).Equals(value)),

            PrimeNgMatchMode.NotEquals =>
                query.Where(x =>
                    x != null &&
                    EF.Property<object>(x, propName) != null &&
                    !EF.Property<object>(x, propName).Equals(value)),

            _ => query
        };
    }

    public static IQueryable<T> ApplyPrimeNgSorting<T>(
        this IQueryable<T> query,
        PrimeNgTableRequest request,
        ILogger? logger = null)
    {
        try
        {
            if (string.IsNullOrEmpty(request.SortField))
                return query;

            var direction = request.SortOrder == 1 ? "asc" : "desc";

            return query.OrderBy($"{request.SortField} {direction}");
        }
        catch (Exception ex)
        {
            logger?.LogWarning("Sort error in {Method}: SortOrder:{SortOrder}, SortField:{SortField}, Error: {Error}",
                nameof(ApplyPrimeNgSorting), request.SortOrder, request.SortField, ex.Message);
            return query;
        }
    }

    public static IQueryable<T> ApplyPrimeNgPaging<T>(
        this IQueryable<T> query,
        PrimeNgTableRequest request,
        int maxRows = 10)
    {
        var rows = request.Rows;
        // rows = 0 → return 0 rows
        if (rows == 0)
            return query.Take(0);

        // Negative rows → take latest N items, capped
        if (rows < 0)
        {
            var take = Math.Min(Math.Abs(rows), maxRows);
            return query.TakeLast(take);
        }

        // Normal paging → take exactly 'rows'
        var skip = Math.Max(0, request.First);
        return query.Skip(skip).Take(rows);
    }
}
