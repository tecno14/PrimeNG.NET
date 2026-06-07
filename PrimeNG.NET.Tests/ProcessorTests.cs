using Microsoft.EntityFrameworkCore;
using PrimeNG.NET.Processing;
using PrimeNG.NET.Requests;
using PrimeNG.NET.Tests.Infrastructure;
using PrimeNG.NET.Tests.TestEntities;

namespace PrimeNG.NET.Tests;

[Collection("SqliteDatabase")]
public class ProcessorTests(SqliteTestFixture fixture)
{
    private TestDbContext Context => fixture.Context;

    [Fact]
    public async Task ProcessAsync_ReturnsFilteredTotalBeforePaging()
    {
        var request = new PrimeNgTableRequest
        {
            First = 0,
            Rows = 2,
            Filters = new Dictionary<string, PrimeNgFilter>
            {
                ["category"] = new() { Value = "Electronics", MatchMode = "equals" }
            }
        };

        var response = await PrimeNgTableProcessor.ProcessAsync(Context.Products.AsQueryable(), request);

        Assert.Equal(2, response.TotalRecords);
        Assert.Equal(2, response.Data.Count());
    }

    [Fact]
    public async Task ProcessAsync_AppliesFilterSortPageTogether()
    {
        var request = new PrimeNgTableRequest
        {
            First = 0,
            Rows = 3,
            SortField = "Price",
            SortOrder = 1,
            Filters = new Dictionary<string, PrimeNgFilter>
            {
                ["category"] = new() { Value = "Sort", MatchMode = "equals" }
            }
        };

        var response = await PrimeNgTableProcessor.ProcessAsync(Context.Products.AsQueryable(), request);

        Assert.Equal(6, response.TotalRecords);
        Assert.Equal([5m, 10m, 20m], response.Data.Select(p => p.Price));
    }

    [Fact]
    public async Task ProcessAsync_EmptyFilters_ReturnsAll()
    {
        var request = new PrimeNgTableRequest
        {
            First = 0,
            Rows = 100,
            SortField = "Id",
            SortOrder = 1
        };

        var response = await PrimeNgTableProcessor.ProcessAsync(Context.Products.AsQueryable(), request);

        Assert.Equal(10, response.TotalRecords);
        Assert.Equal(10, response.Data.Count());
    }
}

[Collection("SqlServerDatabase")]
public class SqlServerProcessorTests(SqlServerTestFixture fixture)
{
    private TestDbContext Context => fixture.Context;

    private void EnsureSqlServerAvailable()
    {
        Skip.IfNot(fixture.IsAvailable, fixture.UnavailableReason ?? "SQL Server is not available.");
    }

    [SkippableFact]
    public async Task ProcessAsync_AppliesNaturalSortOnFilteredSet()
    {
        EnsureSqlServerAvailable();

        var request = new PrimeNgTableRequest
        {
            First = 0,
            Rows = 3,
            SortField = "Name",
            SortOrder = 1,
            Filters = new Dictionary<string, PrimeNgFilter>
            {
                ["category"] = new() { Value = "Sort", MatchMode = "equals" }
            }
        };

        var response = await PrimeNgTableProcessor.ProcessAsync(Context.Products.AsQueryable(), request);

        Assert.Equal(6, response.TotalRecords);
        Assert.Equal(
            TestData.NaturalSortNamesAscending.Take(3),
            response.Data.Select(p => p.Name));
    }
}
