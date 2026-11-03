using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Logging;
using PrimeNG.NET.Requests;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq.Dynamic.Core;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.RegularExpressions;

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

        var entityType = TryGetEntityType(query, typeof(T));

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

                if (ShouldSkipUnmappedMember(entityType, prop, propPath, logger, "filter"))
                    continue;

                // 1. Determine the target type (handling nullables)
                Type targetType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;

                // 2. Parse the filter value to match the property type
                if (!TryConvertFilterValue(targetType, rawValue, out var convertedValue) || convertedValue is null)
                    continue;

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
                    // For Non-strings (Guid, Int, Bool, Enum), only Equals/NotEquals make sense
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

    /// <summary>
    /// Converts a PrimeNG filter string to <paramref name="targetType"/>.
    /// Supports Guid, enums (name or numeric), and <see cref="Convert.ChangeType"/>.
    /// </summary>
    internal static bool TryConvertFilterValue(
        Type targetType,
        string raw,
        [NotNullWhen(true)] out object? value)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        try
        {
            if (targetType == typeof(Guid))
            {
                if (!Guid.TryParse(raw, out var guid))
                    return false;
                value = guid;
                return true;
            }

            if (targetType.IsEnum)
            {
                if (!Enum.TryParse(targetType, raw, ignoreCase: true, out var parsed) || parsed is null)
                    return false;
                value = parsed;
                return true;
            }

            value = Convert.ChangeType(raw, targetType, CultureInfo.InvariantCulture);
            return value is not null;
        }
        catch
        {
            value = null;
            return false;
        }
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

    /// <summary>
    /// Applies PrimeNG table sorting. String fields use portable lexical <c>OrderBy</c> by default.
    /// Pass <paramref name="naturalStringSort"/> to opt into numeric-prefix ordering on SQL Server
    /// and PostgreSQL (Npgsql); other providers fall back to lexical sort.
    /// </summary>
    public static IQueryable<T> ApplyPrimeNgSorting<T>(
      this IQueryable<T> query,
      PrimeNgTableRequest request,
      ILogger? logger = null,
      bool naturalStringSort = false)
    {
        try
        {
            if (string.IsNullOrEmpty(request.SortField))
                return query;

            if (!TryResolvePropertyPath(typeof(T), request.SortField, out var prop, out var propPath))
                return query;

            var entityType = TryGetEntityType(query, typeof(T));
            if (ShouldSkipUnmappedMember(entityType, prop, propPath, logger, "sort"))
                return query;

            var ascending = request.SortOrder == 1;
            var leafType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;

            if (naturalStringSort && leafType == typeof(string))
            {
                var natural = TryApplyNaturalStringSort(query, propPath, ascending, logger);
                if (natural is not null)
                    return natural;
            }

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

    private static IQueryable<T>? TryApplyNaturalStringSort<T>(
        IQueryable<T> query,
        string propPath,
        bool ascending,
        ILogger? logger)
    {
        var providerName = TryGetProviderName(query);
        var param = Expression.Parameter(typeof(T), "x");
        var stringProp = BuildPropertyAccess(param, propPath);

        Expression? keySelector = null;
        if (IsProvider(providerName, "Npgsql"))
            keySelector = BuildPostgresNaturalSortKeyExpression(stringProp);
        else if (IsProvider(providerName, "SqlServer"))
            keySelector = TryBuildSqlServerNaturalSortKeyExpression(stringProp);

        if (keySelector is null)
        {
            logger?.LogWarning(
                "Natural string sort unavailable for provider '{Provider}'; falling back to lexical OrderBy.",
                providerName ?? "(unknown)");
            return null;
        }

        var keyLambda = Expression.Lambda<Func<T, int>>(keySelector, param);
        var stringLambda = Expression.Lambda<Func<T, string>>(stringProp, param);
        return ascending
            ? query.OrderBy(keyLambda).ThenBy(stringLambda)
            : query.OrderByDescending(keyLambda).ThenByDescending(stringLambda);
    }

    private static string? TryGetProviderName(IQueryable query)
    {
        try
        {
            var context = TryGetDbContext(query);
            if (context?.Database.ProviderName is { } providerName)
                return providerName;

            // EF Core 10: EntityQueryProvider is not IInfrastructure — infer from QueryCompiler deps.
            return TryInferProviderNameFromQueryCompiler(query.Provider);
        }
        catch
        {
            return null;
        }
    }

    private static DbContext? TryGetDbContext(IQueryable query)
    {
        try
        {
            // DbSet implements IInfrastructure; composed queries (Where/OrderBy) often do not.
            if (query is IInfrastructure<IServiceProvider> queryInfra)
            {
                var fromQuery = queryInfra.GetService<ICurrentDbContext>()?.Context;
                if (fromQuery is not null)
                    return fromQuery;
            }

            if (query.Provider is IInfrastructure<IServiceProvider> providerInfra)
            {
                var fromProvider = providerInfra.GetService<ICurrentDbContext>()?.Context;
                if (fromProvider is not null)
                    return fromProvider;
            }

            // EF Core 10: EntityQueryProvider is not IInfrastructure — dig into QueryCompiler.
            return TryGetDbContextFromQueryCompiler(query.Provider);
        }
        catch
        {
            // ignore
        }

        return null;
    }

    private static DbContext? TryGetDbContextFromQueryCompiler(IQueryProvider provider)
    {
        const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
        var compiler = provider.GetType().GetField("_queryCompiler", flags)?.GetValue(provider);
        if (compiler is null)
            return null;

        foreach (var field in compiler.GetType().GetFields(flags))
        {
            var value = field.GetValue(compiler);
            switch (value)
            {
                case ICurrentDbContext current:
                    return current.Context;
                case DbContext db:
                    return db;
            }
        }

        // QueryCompilerDependencies / similar often expose Context via a Dependencies object.
        foreach (var field in compiler.GetType().GetFields(flags))
        {
            var deps = field.GetValue(compiler);
            if (deps is null)
                continue;

            foreach (var depField in deps.GetType().GetFields(flags))
            {
                if (depField.GetValue(deps) is ICurrentDbContext current)
                    return current.Context;
            }

            foreach (var depProp in deps.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (depProp.GetValue(deps) is ICurrentDbContext current)
                    return current.Context;
            }
        }

        return null;
    }

    private static IEntityType? TryGetEntityType(IQueryable query, Type clrType)
    {
        var context = TryGetDbContext(query);
        if (context is null)
            return null;

        return context.Model.FindEntityType(clrType);
    }

    /// <summary>
    /// Skip CLR members that EF cannot translate: unmapped model properties, or get-only
    /// computed members when the EF model is unavailable (composed queries).
    /// </summary>
    private static bool ShouldSkipUnmappedMember(
        IEntityType? entityType,
        PropertyInfo leafProperty,
        string propPath,
        ILogger? logger,
        string action)
    {
        if (entityType is not null)
        {
            if (IsEfMappedPath(entityType, propPath))
                return false;

            logger?.LogWarning(
                "Skipping {Action} on unmapped property '{Path}'.",
                action,
                propPath);
            return true;
        }

        // No EF entity type (composed query / projection): skip get-only computed members.
        if (IsGetOnlyComputedMember(leafProperty))
        {
            logger?.LogWarning(
                "Skipping {Action} on get-only property '{Path}' (likely unmapped).",
                action,
                propPath);
            return true;
        }

        return false;
    }

    private static bool IsGetOnlyComputedMember(PropertyInfo property) =>
        property.GetSetMethod(nonPublic: true) is null;

    /// <summary>
    /// True when every segment of <paramref name="propPath"/> is an EF-mapped property or navigation.
    /// </summary>
    internal static bool IsEfMappedPath(IEntityType entityType, string propPath)
    {
        var current = entityType;
        var segments = propPath.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length == 0)
            return false;

        for (var i = 0; i < segments.Length; i++)
        {
            var name = segments[i];
            var isLast = i == segments.Length - 1;

            var property = current.FindProperty(name);
            if (property is not null)
                return isLast;

            var navigation = current.FindNavigation(name);
            if (navigation is null)
                return false;

            current = navigation.TargetEntityType;
        }

        return true;
    }

    private static string? TryInferProviderNameFromQueryCompiler(IQueryProvider provider)
    {
        const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
        var compiler = provider.GetType().GetField("_queryCompiler", flags)?.GetValue(provider);
        if (compiler is null)
            return null;

        foreach (var fieldName in new[] { "_compiledQueryCacheKeyGenerator", "_evaluatableExpressionFilter" })
        {
            var value = compiler.GetType().GetField(fieldName, flags)?.GetValue(compiler);
            var typeName = value?.GetType().FullName;
            if (typeName is null)
                continue;

            if (typeName.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
                return "Npgsql.EntityFrameworkCore.PostgreSQL";
            if (typeName.Contains("SqlServer", StringComparison.OrdinalIgnoreCase))
                return "Microsoft.EntityFrameworkCore.SqlServer";
            if (typeName.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
                return "Microsoft.EntityFrameworkCore.Sqlite";
        }

        return null;
    }

    private static bool IsProvider(string? providerName, string token) =>
        providerName is not null
        && providerName.Contains(token, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Npgsql translates <see cref="Regex.IsMatch(string, string)"/> and
    /// <see cref="Regex.Replace(string, string, string)"/>. Use PostgreSQL backrefs (<c>\1</c>).
    /// </summary>
    private static Expression BuildPostgresNaturalSortKeyExpression(Expression stringProp)
    {
        var isMatch = typeof(Regex).GetMethod(
            nameof(Regex.IsMatch),
            [typeof(string), typeof(string)])!;
        var replace = typeof(Regex).GetMethod(
            nameof(Regex.Replace),
            [typeof(string), typeof(string), typeof(string)])!;
        var toInt32 = typeof(Convert).GetMethod(nameof(Convert.ToInt32), [typeof(string)])!;

        var hasLeadingDigits = Expression.Call(
            isMatch,
            stringProp,
            Expression.Constant(@"^\d"));

        var digitPrefix = Expression.Call(
            replace,
            stringProp,
            Expression.Constant(@"^(\d+).*$"),
            Expression.Constant(@"\1"));

        return Expression.Condition(
            hasLeadingDigits,
            Expression.Call(toInt32, digitPrefix),
            Expression.Constant(NaturalSortKey.NonNumericPrefix));
    }

    /// <summary>
    /// Optional SQL Server path via reflection so this library does not depend on EF SqlServer.
    /// </summary>
    private static Expression? TryBuildSqlServerNaturalSortKeyExpression(Expression stringProp)
    {
        var extensionsType = Type.GetType(
            "Microsoft.EntityFrameworkCore.SqlServerDbFunctionsExtensions, Microsoft.EntityFrameworkCore.SqlServer");
        var patIndexMethod = extensionsType?.GetMethod(
            "PatIndex",
            [typeof(DbFunctions), typeof(string), typeof(string)]);
        if (patIndexMethod is null)
            return null;

        var suffix = Expression.Call(
            typeof(string).GetMethod(nameof(string.Concat), [typeof(string), typeof(string)])!,
            stringProp,
            Expression.Constant("X"));

        var patIndex = Expression.Call(
            patIndexMethod,
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
            Expression.Constant(NaturalSortKey.NonNumericPrefix),
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
