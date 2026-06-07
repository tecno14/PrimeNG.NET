using Microsoft.EntityFrameworkCore;
using PrimeNG.NET.Extensions;
using PrimeNG.NET.Requests;
using PrimeNG.NET.Tests.Infrastructure;
using PrimeNG.NET.Tests.TestEntities;

namespace PrimeNG.NET.Tests;

[Collection("SqliteDatabase")]
public class PagingTests(SqliteTestFixture fixture)
{
    private TestDbContext Context => fixture.Context;

    private IQueryable<TestProduct> OrderedQuery =>
        Context.Products.OrderBy(p => p.Id);

    [Fact]
    public async Task NormalPaging_SkipTake()
    {
        var request = new PrimeNgTableRequest { First = 2, Rows = 3 };

        var results = await OrderedQuery
            .ApplyPrimeNgPaging(request)
            .Select(p => p.Id)
            .ToListAsync();

        Assert.Equal([3, 4, 5], results);
    }

    [Fact]
    public async Task ZeroRows_ReturnsEmpty()
    {
        var request = new PrimeNgTableRequest { First = 0, Rows = 0 };

        var results = await OrderedQuery
            .ApplyPrimeNgPaging(request)
            .ToListAsync();

        Assert.Empty(results);
    }

    [Fact]
    public async Task NegativeFirst_ClampedToZero()
    {
        var request = new PrimeNgTableRequest { First = -1, Rows = 2 };

        var results = await OrderedQuery
            .ApplyPrimeNgPaging(request)
            .Select(p => p.Id)
            .ToListAsync();

        Assert.Equal([1, 2], results);
    }
}

[Collection("SqlServerDatabase")]
public class SqlServerPagingTests(SqlServerTestFixture fixture)
{
    private TestDbContext Context => fixture.Context;

    private void EnsureSqlServerAvailable()
    {
        Skip.IfNot(fixture.IsAvailable, fixture.UnavailableReason ?? "SQL Server is not available.");
    }

    [SkippableFact]
    public async Task NegativeRows_TakeLastCapped()
    {
        EnsureSqlServerAvailable();

        var request = new PrimeNgTableRequest { First = 0, Rows = -5 };

        var results = await Context.Products
            .OrderBy(p => p.Id)
            .ApplyPrimeNgPaging(request, maxRows: 3)
            .Select(p => p.Id)
            .ToListAsync();

        Assert.Equal([8, 9, 10], results);
    }
}
