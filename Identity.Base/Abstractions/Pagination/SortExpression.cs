using System;

namespace Identity.Base.Abstractions.Pagination;

public readonly record struct SortExpression(string Field, SortDirection Direction)
{
    public static SortExpression From(string value)
    {
        if (!TryParse(value, out var expression))
        {
            throw new ArgumentException($"Sort expression '{value}' is invalid.", nameof(value));
        }

        return expression;
    }

    public static bool TryParse(string? value, out SortExpression expression)
    {
        expression = default;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();

        var direction = SortDirection.Ascending;
        string field;

        if (trimmed.StartsWith("-", StringComparison.Ordinal))
        {
            direction = SortDirection.Descending;
            field = trimmed[1..];
        }
        else
        {
            if (trimmed.StartsWith(":", StringComparison.Ordinal))
            {
                return false;
            }

            var parts = trimmed.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            field = parts[0];

            if (parts.Length > 1)
            {
                direction = parts[1].Equals("desc", StringComparison.OrdinalIgnoreCase)
                    ? SortDirection.Descending
                    : SortDirection.Ascending;
            }
        }

        if (string.IsNullOrWhiteSpace(field) || field.StartsWith(":", StringComparison.Ordinal))
        {
            return false;
        }

        expression = new SortExpression(field, direction);
        return true;
    }
}
