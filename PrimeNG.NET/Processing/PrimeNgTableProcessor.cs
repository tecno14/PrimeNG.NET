using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PrimeNG.NET.Extensions;
using PrimeNG.NET.Requests;
using PrimeNG.NET.Responses;

namespace PrimeNG.NET.Processing;

public static class PrimeNgTableProcessor
{
    public static async Task<PrimeNgTableResponse<T>> ProcessAsync<T>(
        IQueryable<T> query,
        PrimeNgTableRequest request,
        ILogger? logger = null)
    {
        var filteredQuery = query.ApplyPrimeNgFiltering(request, logger);

        var total = await filteredQuery.CountAsync();

        var sortedQuery = filteredQuery.ApplyPrimeNgSorting(request, logger);

        var pagedQuery = sortedQuery.ApplyPrimeNgPaging(request);

        var data = await pagedQuery.ToListAsync();

        return new PrimeNgTableResponse<T>
        {
            Data = data,
            TotalRecords = total
        };
    }
}
