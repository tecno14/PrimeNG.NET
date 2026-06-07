namespace PrimeNG.NET.Extensions;

internal static class NaturalSortKey
{
    internal const int NonNumericPrefix = int.MaxValue;

    /// <summary>
    /// Sort key for natural (numeric-prefix) ordering of strings.
    /// Mirrors the SQL Server expression used in <see cref="PrimeNgQueryableExtensions"/>.
    /// </summary>
    internal static int FromString(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return NonNumericPrefix;

        var firstNonDigit = FirstNonDigitIndex(value + "X");
        if (firstNonDigit <= 1)
            return NonNumericPrefix;

        var prefix = value[..Math.Min(firstNonDigit - 1, value.Length)];
        return int.TryParse(prefix, out var number) ? number : NonNumericPrefix;
    }

    private static int FirstNonDigitIndex(string value)
    {
        for (var i = 0; i < value.Length; i++)
        {
            if (!char.IsDigit(value[i]))
                return i + 1;
        }

        return value.Length + 1;
    }
}
