using Microsoft.EntityFrameworkCore;
using PrimeNG.NET.Extensions;
using PrimeNG.NET.Requests;
using PrimeNG.NET.Tests.Infrastructure;
using PrimeNG.NET.Tests.TestEntities;

namespace PrimeNG.NET.Tests;

[Collection("SqliteDatabase")]
public class FilteringTests(SqliteTestFixture fixture)
{
    private TestDbContext Context => fixture.Context;

    [Fact]
    public async Task Contains_FiltersStringColumn()
    {
        var request = new PrimeNgTableRequest
        {
            Filters = new Dictionary<string, PrimeNgFilter>
            {
                ["name"] = new() { Value = "lap", MatchMode = "contains" }
            }
        };

        var results = await Context.Products
            .ApplyPrimeNgFiltering(request)
            .Select(p => p.Name)
            .ToListAsync();

        Assert.Single(results);
        Assert.Equal("Laptop Pro", results[0]);
    }

    [Fact]
    public async Task Contains_IsCaseInsensitive()
    {
        var request = new PrimeNgTableRequest
        {
            Filters = new Dictionary<string, PrimeNgFilter>
            {
                ["name"] = new() { Value = "AMP", MatchMode = "contains" }
            }
        };

        var results = await Context.Products
            .ApplyPrimeNgFiltering(request)
            .Select(p => p.Name)
            .ToListAsync();

        Assert.Single(results);
        Assert.Equal("Desk Lamp", results[0]);
    }

    [Fact]
    public async Task StartsWith_FiltersStringColumn()
    {
        var request = new PrimeNgTableRequest
        {
            Filters = new Dictionary<string, PrimeNgFilter>
            {
                ["name"] = new() { Value = "desk", MatchMode = "startsWith" }
            }
        };

        var results = await Context.Products
            .ApplyPrimeNgFiltering(request)
            .Select(p => p.Name)
            .ToListAsync();

        Assert.Single(results);
        Assert.Equal("Desk Lamp", results[0]);
    }

    [Fact]
    public async Task EndsWith_FiltersStringColumn()
    {
        var request = new PrimeNgTableRequest
        {
            Filters = new Dictionary<string, PrimeNgFilter>
            {
                ["name"] = new() { Value = "mouse", MatchMode = "endsWith" }
            }
        };

        var results = await Context.Products
            .ApplyPrimeNgFiltering(request)
            .Select(p => p.Name)
            .ToListAsync();

        Assert.Single(results);
        Assert.Equal("Wireless Mouse", results[0]);
    }

    [Fact]
    public async Task NotContains_ExcludesMatches()
    {
        var request = new PrimeNgTableRequest
        {
            Filters = new Dictionary<string, PrimeNgFilter>
            {
                ["category"] = new() { Value = "sort", MatchMode = "notContains" }
            }
        };

        var results = await Context.Products
            .ApplyPrimeNgFiltering(request)
            .Select(p => p.Name)
            .ToListAsync();

        Assert.DoesNotContain(results, n => n is "1 alpha" or "apple");
        Assert.Contains("Laptop Pro", results);
    }

    [Fact]
    public async Task Equals_String_IsCaseInsensitive()
    {
        var request = new PrimeNgTableRequest
        {
            Filters = new Dictionary<string, PrimeNgFilter>
            {
                ["name"] = new() { Value = "LAPTOP PRO", MatchMode = "equals" }
            }
        };

        var results = await Context.Products
            .ApplyPrimeNgFiltering(request)
            .Select(p => p.Name)
            .ToListAsync();

        Assert.Single(results);
        Assert.Equal("Laptop Pro", results[0]);
    }

    [Fact]
    public async Task Equals_NumericProperty()
    {
        var request = new PrimeNgTableRequest
        {
            Filters = new Dictionary<string, PrimeNgFilter>
            {
                ["price"] = new() { Value = "49.99", MatchMode = "equals" }
            }
        };

        var results = await Context.Products
            .ApplyPrimeNgFiltering(request)
            .Select(p => p.Name)
            .ToListAsync();

        Assert.Single(results);
        Assert.Equal("Desk Lamp", results[0]);
    }

    [Fact]
    public async Task NotEquals_NumericProperty()
    {
        var request = new PrimeNgTableRequest
        {
            Filters = new Dictionary<string, PrimeNgFilter>
            {
                ["price"] = new() { Value = "49.99", MatchMode = "notEquals" }
            }
        };

        var count = await Context.Products
            .ApplyPrimeNgFiltering(request)
            .CountAsync();

        Assert.Equal(9, count);
    }

    [Fact]
    public async Task Equals_BoolProperty()
    {
        var request = new PrimeNgTableRequest
        {
            Filters = new Dictionary<string, PrimeNgFilter>
            {
                ["isActive"] = new() { Value = "false", MatchMode = "equals" }
            }
        };

        var results = await Context.Products
            .ApplyPrimeNgFiltering(request)
            .Select(p => p.Name)
            .OrderBy(n => n)
            .ToListAsync();

        Assert.Equal(["10 gamma", "20 delta", "Wireless Mouse"], results);
    }

    [Fact]
    public async Task Equals_GuidProperty()
    {
        var request = new PrimeNgTableRequest
        {
            Filters = new Dictionary<string, PrimeNgFilter>
            {
                ["sku"] = new() { Value = TestData.SkuAlpha.ToString(), MatchMode = "equals" }
            }
        };

        var results = await Context.Products
            .ApplyPrimeNgFiltering(request)
            .Select(p => p.Name)
            .ToListAsync();

        Assert.Single(results);
        Assert.Equal("1 alpha", results[0]);
    }

    [Fact]
    public async Task SkipsEmptyFilterValues()
    {
        var request = new PrimeNgTableRequest
        {
            Filters = new Dictionary<string, PrimeNgFilter>
            {
                ["name"] = new() { Value = "   ", MatchMode = "contains" }
            }
        };

        var count = await Context.Products
            .ApplyPrimeNgFiltering(request)
            .CountAsync();

        Assert.Equal(10, count);
    }

    [Fact]
    public async Task SkipsUnknownProperty()
    {
        var request = new PrimeNgTableRequest
        {
            Filters = new Dictionary<string, PrimeNgFilter>
            {
                ["unknownField"] = new() { Value = "x", MatchMode = "equals" }
            }
        };

        var count = await Context.Products
            .ApplyPrimeNgFiltering(request)
            .CountAsync();

        Assert.Equal(10, count);
    }

    [Fact]
    public async Task SkipsInvalidConversion()
    {
        var request = new PrimeNgTableRequest
        {
            Filters = new Dictionary<string, PrimeNgFilter>
            {
                ["quantity"] = new() { Value = "abc", MatchMode = "equals" }
            }
        };

        var count = await Context.Products
            .ApplyPrimeNgFiltering(request)
            .CountAsync();

        Assert.Equal(10, count);
    }

    [Fact]
    public async Task Equals_EnumProperty_ByName_IsCaseInsensitive()
    {
        var request = new PrimeNgTableRequest
        {
            Filters = new Dictionary<string, PrimeNgFilter>
            {
                ["kind"] = new() { Value = "premium", MatchMode = "equals" }
            }
        };

        var results = await Context.Products
            .ApplyPrimeNgFiltering(request)
            .Select(p => p.Name)
            .OrderBy(n => n)
            .ToListAsync();

        Assert.Equal(["09 item", "Laptop Pro"], results);
    }

    [Fact]
    public async Task Equals_EnumProperty_ByNumericString()
    {
        var request = new PrimeNgTableRequest
        {
            Filters = new Dictionary<string, PrimeNgFilter>
            {
                ["kind"] = new() { Value = "2", MatchMode = "equals" }
            }
        };

        var count = await Context.Products
            .ApplyPrimeNgFiltering(request)
            .CountAsync();

        Assert.Equal(3, count); // Clearance
    }

    [Fact]
    public async Task NotEquals_EnumProperty()
    {
        var request = new PrimeNgTableRequest
        {
            Filters = new Dictionary<string, PrimeNgFilter>
            {
                ["kind"] = new() { Value = "Standard", MatchMode = "notEquals" }
            }
        };

        var count = await Context.Products
            .ApplyPrimeNgFiltering(request)
            .CountAsync();

        Assert.Equal(5, count); // Premium(2) + Clearance(3)
    }

    [Fact]
    public async Task SkipsUnmappedComputedProperty_Filter()
    {
        var request = new PrimeNgTableRequest
        {
            Filters = new Dictionary<string, PrimeNgFilter>
            {
                ["displayLabel"] = new() { Value = "Laptop", MatchMode = "contains" }
            }
        };

        var count = await Context.Products
            .ApplyPrimeNgFiltering(request)
            .CountAsync();

        Assert.Equal(10, count);
    }

    [Theory]
    [InlineData(typeof(TestProductKind), "Premium", true)]
    [InlineData(typeof(TestProductKind), "premium", true)]
    [InlineData(typeof(TestProductKind), "1", true)]
    [InlineData(typeof(TestProductKind), "nope", false)]
    [InlineData(typeof(Guid), "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", true)]
    [InlineData(typeof(Guid), "not-a-guid", false)]
    public void TryConvertFilterValue_HandlesEnumsAndGuids(Type targetType, string raw, bool expected)
    {
        var ok = PrimeNgQueryableExtensions.TryConvertFilterValue(targetType, raw, out var value);
        Assert.Equal(expected, ok);
        if (expected)
            Assert.NotNull(value);
    }
}
