using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PrimeNG.NET.Extensions;
using PrimeNG.NET.Requests;
using PrimeNG.NET.Responses;

namespace PrimeNG.NET.Processing;

public static class PrimeNgTableProcessor
{
    /// <param name="naturalStringSort">
    /// When true, string sort fields use numeric-prefix order on SQL Server and PostgreSQL (Npgsql).
    /// Default is portable lexical <c>OrderBy</c>.
    /// </param>
    public static async Task<PrimeNgTableResponse<T>> ProcessAsync<T>(
        IQueryable<T> query,
        PrimeNgTableRequest request,
        ILogger? logger = null,
        bool naturalStringSort = false)
    {
        var filteredQuery = query.ApplyPrimeNgFiltering(request, logger);

        var total = await filteredQuery.CountAsync();

        var sortedQuery = filteredQuery.ApplyPrimeNgSorting(request, logger, naturalStringSort);

        var pagedQuery = sortedQuery.ApplyPrimeNgPaging(request);

        var data = await pagedQuery.ToListAsync();

        return new PrimeNgTableResponse<T>
        {
            Data = data,
            TotalRecords = total
        };
    }
}
