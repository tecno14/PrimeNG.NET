using Microsoft.EntityFrameworkCore;
using PrimeNG.NET.Extensions;
using PrimeNG.NET.Requests;
using PrimeNG.NET.Tests.Infrastructure;
using PrimeNG.NET.Tests.TestEntities;

namespace PrimeNG.NET.Tests;

[Collection("SqliteDatabase")]
public class SortingTests(SqliteTestFixture fixture)
{
    private TestDbContext Context => fixture.Context;

    [Fact]
    public async Task Numeric_Sort_Ascending()
    {
        var request = new PrimeNgTableRequest { SortField = "Price", SortOrder = 1 };

        var results = await Context.Products
            .ApplyPrimeNgSorting(request)
            .Select(p => p.Price)
            .ToListAsync();

        Assert.Equal(results.OrderBy(p => p).ToList(), results);
    }

    [Fact]
    public async Task Numeric_Sort_Descending()
    {
        var request = new PrimeNgTableRequest { SortField = "Price", SortOrder = -1 };

        var results = await Context.Products
            .ApplyPrimeNgSorting(request)
            .Select(p => p.Price)
            .ToListAsync();

        Assert.Equal(results.OrderByDescending(p => p).ToList(), results);
    }

    [Fact]
    public async Task NullSortField_ReturnsOriginalOrder()
    {
        var request = new PrimeNgTableRequest { SortField = null, SortOrder = 1 };

        var withSort = await Context.Products
            .OrderBy(p => p.Id)
            .ApplyPrimeNgSorting(request)
            .Select(p => p.Id)
            .ToListAsync();

        var withoutSort = await Context.Products
            .OrderBy(p => p.Id)
            .Select(p => p.Id)
            .ToListAsync();

        Assert.Equal(withoutSort, withSort);
    }

    [Fact]
    public async Task InvalidSortField_DoesNotThrow()
    {
        var request = new PrimeNgTableRequest { SortField = "NonExistent", SortOrder = 1 };

        var results = await Context.Products
            .OrderBy(p => p.Id)
            .ApplyPrimeNgSorting(request)
            .Select(p => p.Id)
            .ToListAsync();

        Assert.Equal(10, results.Count);
    }
}

[Collection("SqlServerDatabase")]
public class SqlServerNaturalSortTests(SqlServerTestFixture fixture)
{
    private TestDbContext Context => fixture.Context;

    private void EnsureSqlServerAvailable()
    {
        Skip.IfNot(fixture.IsAvailable, fixture.UnavailableReason ?? "SQL Server is not available.");
    }

    [SkippableFact]
    public async Task String_NaturalSort_Ascending()
    {
        EnsureSqlServerAvailable();

        var request = new PrimeNgTableRequest { SortField = "Name", SortOrder = 1 };

        var results = await Context.Products
            .Where(p => p.Category == "Sort")
            .ApplyPrimeNgSorting(request)
            .Select(p => p.Name)
            .ToListAsync();

        Assert.Equal(TestData.NaturalSortNamesAscending, results);
    }

    [SkippableFact]
    public async Task String_NaturalSort_Descending()
    {
        EnsureSqlServerAvailable();

        var request = new PrimeNgTableRequest { SortField = "Name", SortOrder = -1 };

        var results = await Context.Products
            .Where(p => p.Category == "Sort")
            .ApplyPrimeNgSorting(request)
            .Select(p => p.Name)
            .ToListAsync();

        Assert.Equal(TestData.NaturalSortNamesAscending.Reverse().ToList(), results);
    }

    [SkippableFact]
    public async Task String_NaturalSort_CaseInsensitiveSortField()
    {
        EnsureSqlServerAvailable();

        var request = new PrimeNgTableRequest { SortField = "name", SortOrder = 1 };

        var results = await Context.Products
            .Where(p => p.Category == "Sort")
            .ApplyPrimeNgSorting(request)
            .Select(p => p.Name)
            .ToListAsync();

        Assert.Equal(TestData.NaturalSortNamesAscending, results);
    }
}
