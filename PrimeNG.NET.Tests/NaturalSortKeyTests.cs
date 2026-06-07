using PrimeNG.NET.Extensions;

namespace PrimeNG.NET.Tests;

public class NaturalSortKeyTests
{
    [Theory]
    [InlineData("1 alpha", 1)]
    [InlineData("2 beta", 2)]
    [InlineData("10 gamma", 10)]
    [InlineData("09 item", 9)]
    [InlineData("20 delta", 20)]
    [InlineData("apple", NaturalSortKey.NonNumericPrefix)]
    [InlineData("", NaturalSortKey.NonNumericPrefix)]
    public void FromString_ReturnsExpectedNumericPrefix(string input, int expected)
    {
        Assert.Equal(expected, NaturalSortKey.FromString(input));
    }

    [Fact]
    public void FromString_OrdersNumericPrefixesCorrectly()
    {
        var names = new[] { "10 gamma", "2 beta", "1 alpha", "apple", "09 item", "20 delta" };

        var ordered = names
            .OrderBy(NaturalSortKey.FromString)
            .ThenBy(n => n, StringComparer.Ordinal)
            .ToList();

        Assert.Equal(
            ["1 alpha", "2 beta", "09 item", "10 gamma", "20 delta", "apple"],
            ordered);
    }
}
