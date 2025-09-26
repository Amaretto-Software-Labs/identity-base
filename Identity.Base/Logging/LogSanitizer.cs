using System;
using System.Linq;

namespace Identity.Base.Logging;

public sealed class LogSanitizer : ILogSanitizer
{
    private const string Redacted = "[redacted]";

    public string? RedactEmail(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        var atIndex = value.IndexOf('@');
        if (atIndex <= 0 || atIndex == value.Length - 1)
        {
            return Redacted;
        }

        var local = value[..atIndex];
        var domain = value[(atIndex + 1)..];

        if (local.Length <= 2)
        {
            return $"{new string('*', local.Length)}@{domain}";
        }

        return $"{local[0]}***{local[^1]}@{domain}";
    }

    public string? RedactPhoneNumber(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        var digits = new string(value.Where(char.IsDigit).ToArray());
        if (digits.Length == 0)
        {
            return Redacted;
        }

        var visibleLength = Math.Min(4, digits.Length);
        var suffix = digits[^visibleLength..];
        return $"***{suffix}";
    }

    public string RedactToken(string? value)
        => Redacted;
}
