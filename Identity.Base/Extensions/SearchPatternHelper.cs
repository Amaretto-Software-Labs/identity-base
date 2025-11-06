using System;

namespace Identity.Base.Extensions;

public static class SearchPatternHelper
{
    public static string CreateSearchPattern(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "%";
        }

        var trimmed = value.Trim();

        var escaped = trimmed
            .Replace(@"\", @"\\", StringComparison.Ordinal)
            .Replace("%", @"\%", StringComparison.Ordinal)
            .Replace("_", @"\_", StringComparison.Ordinal);

        return $"%{escaped}%";
    }
}
