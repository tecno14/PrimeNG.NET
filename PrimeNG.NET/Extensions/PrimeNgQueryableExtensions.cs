using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PrimeNG.NET.Requests;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Dynamic.Core;
using System.Linq.Expressions;
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

                // Case-insensitive property lookup (supports nested paths like "Name.First")
                if (!TryResolvePropertyPath(typeof(T), filter.Key, out var prop, out var propPath))
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
                    query = ApplyStringFilter(query, propPath, mode, val);
                }
                else
                {
                    // For Non-strings (Guid, Int, Bool), only Equals/NotEquals make sense
                    query = ApplyExactFilter(query, propPath, mode, convertedValue);
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
    string propPath,
    PrimeNgMatchMode mode,
    string value)
    {
        if (string.IsNullOrEmpty(value))
            return query;

        var param = Expression.Parameter(typeof(T), "x");
        var property = BuildPropertyAccess(param, propPath);
        var toLower = Expression.Call(
            property,
            typeof(string).GetMethod(nameof(string.ToLower), Type.EmptyTypes)!);

        var likeMethod = typeof(DbFunctionsExtensions).GetMethod(
            nameof(DbFunctionsExtensions.Like),
            [typeof(DbFunctions), typeof(string), typeof(string)])!;
        var efFunctions = Expression.Property(null, typeof(EF), nameof(EF.Functions));

        Expression Like(string pattern) =>
            Expression.Call(likeMethod, efFunctions, toLower, Expression.Constant(pattern));

        Expression? predicate = mode switch
        {
            PrimeNgMatchMode.Contains => Like($"%{value}%"),
            PrimeNgMatchMode.NotContains => Expression.Not(Like($"%{value}%")),
            PrimeNgMatchMode.StartsWith => Like($"{value}%"),
            PrimeNgMatchMode.EndsWith => Like($"%{value}"),
            PrimeNgMatchMode.Equals => Expression.Equal(toLower, Expression.Constant(value)),
            _ => null
        };

        if (predicate is null)
            return query;

        if (!typeof(T).IsValueType)
        {
            predicate = Expression.AndAlso(
                Expression.NotEqual(param, Expression.Constant(null, typeof(T))),
                predicate);
        }

        return query.Where(Expression.Lambda<Func<T, bool>>(predicate, param));
    }

    private static IQueryable<T> ApplyExactFilter<T>(
        IQueryable<T> query,
        string propPath,
        PrimeNgMatchMode mode,
        object value)
    {
        if (value is null)
            return query;

        var param = Expression.Parameter(typeof(T), "x");
        var property = BuildPropertyAccess(param, propPath);
        var typedConstant = Expression.Convert(Expression.Constant(value), property.Type);

        Expression? predicate = mode switch
        {
            PrimeNgMatchMode.Equals => Expression.Equal(property, typedConstant),
            PrimeNgMatchMode.NotEquals => Expression.NotEqual(property, typedConstant),
            _ => null
        };

        if (predicate is null)
            return query;

        if (!typeof(T).IsValueType)
        {
            predicate = Expression.AndAlso(
                Expression.NotEqual(param, Expression.Constant(null, typeof(T))),
                predicate);
        }

        return query.Where(Expression.Lambda<Func<T, bool>>(predicate, param));
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

            if (!TryResolvePropertyPath(typeof(T), request.SortField, out var prop, out var propPath))
                return query;

            var ascending = request.SortOrder == 1;
            var leafType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;

            if (leafType == typeof(string))
                return ApplyNaturalStringSort(query, propPath, ascending);

            var direction = ascending ? "asc" : "desc";
            return query.OrderBy($"{propPath} {direction}");
        }
        catch (Exception ex)
        {
            logger?.LogWarning("Sort error in {Method}: SortOrder:{SortOrder}, SortField:{SortField}, Error: {Error}",
                nameof(ApplyPrimeNgSorting), request.SortOrder, request.SortField, ex.Message);
            return query;
        }
    }

    private static IQueryable<T> ApplyNaturalStringSort<T>(
        IQueryable<T> query,
        string propPath,
        bool ascending)
    {
        var param = Expression.Parameter(typeof(T), "x");
        var stringProp = BuildPropertyAccess(param, propPath);

        var keySelector = BuildNaturalSortKeyExpression(stringProp);
        var keyLambda = Expression.Lambda<Func<T, int>>(keySelector, param);
        var stringLambda = Expression.Lambda<Func<T, string>>(stringProp, param);

        return ascending
            ? query.OrderBy(keyLambda).ThenBy(stringLambda)
            : query.OrderByDescending(keyLambda).ThenByDescending(stringLambda);
    }

    private static Expression BuildNaturalSortKeyExpression(Expression stringProp)
    {
        var suffix = Expression.Call(
            typeof(string).GetMethod(nameof(string.Concat), [typeof(string), typeof(string)])!,
            stringProp,
            Expression.Constant("X"));

        var patIndex = Expression.Call(
            typeof(SqlServerDbFunctionsExtensions).GetMethod(
                nameof(SqlServerDbFunctionsExtensions.PatIndex),
                [typeof(DbFunctions), typeof(string), typeof(string)])!,
            Expression.Property(null, typeof(EF), nameof(EF.Functions)),
            Expression.Constant("%[^0-9]%"),
            suffix);

        var length = Expression.Subtract(
            Expression.Convert(patIndex, typeof(int)),
            Expression.Constant(1));

        var prefix = Expression.Call(
            stringProp,
            typeof(string).GetMethod(nameof(string.Substring), [typeof(int), typeof(int)])!,
            Expression.Constant(0),
            length);

        return Expression.Condition(
            Expression.Equal(patIndex, Expression.Constant(1L)),
            Expression.Constant(int.MaxValue),
            Expression.Call(
                typeof(Convert).GetMethod(nameof(Convert.ToInt32), [typeof(string)])!,
                prefix));
    }

    /// <summary>
    /// Resolves a dotted property path (e.g. "Name.First") with case-insensitive segment matching.
    /// </summary>
    private static bool TryResolvePropertyPath(
        Type rootType,
        string path,
        [NotNullWhen(true)] out PropertyInfo? leafProperty,
        [NotNullWhen(true)] out string? resolvedPath)
    {
        leafProperty = null;
        resolvedPath = null;

        if (string.IsNullOrWhiteSpace(path))
            return false;

        var segments = path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length == 0)
            return false;

        var currentType = rootType;
        PropertyInfo? prop = null;
        var resolved = new string[segments.Length];

        for (var i = 0; i < segments.Length; i++)
        {
            prop = currentType.GetProperty(
                segments[i],
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

            if (prop is null)
                return false;

            resolved[i] = prop.Name;
            currentType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
        }

        leafProperty = prop!;
        resolvedPath = string.Join('.', resolved);
        return true;
    }

    private static Expression BuildPropertyAccess(Expression instance, string propertyPath)
    {
        Expression access = instance;
        foreach (var segment in propertyPath.Split('.'))
        {
            var prop = access.Type.GetProperty(
                segment,
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)
                ?? throw new InvalidOperationException(
                    $"Property '{segment}' not found on type '{access.Type.Name}'.");

            access = Expression.Property(access, prop);
        }

        return access;
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
