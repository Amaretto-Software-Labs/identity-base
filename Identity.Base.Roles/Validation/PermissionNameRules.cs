using System.Text.RegularExpressions;

namespace Identity.Base.Roles.Validation;

public static partial class PermissionNameRules
{
    public const int MaxLength = 128;
    public const string ValidationMessage = "Permission names must use lowercase letters, digits, '.', '-', or '_' and cannot contain spaces.";

    public static bool IsValid(string? permissionName)
    {
        if (string.IsNullOrWhiteSpace(permissionName))
        {
            return false;
        }

        var trimmed = permissionName.Trim();
        return trimmed.Length <= MaxLength && PermissionNameRegex().IsMatch(trimmed);
    }

    public static string? GetValidationError(string? permissionName)
    {
        if (string.IsNullOrWhiteSpace(permissionName))
        {
            return "Permission name is required.";
        }

        var trimmed = permissionName.Trim();
        if (trimmed.Length > MaxLength)
        {
            return $"Permission name exceeds the maximum length of {MaxLength} characters.";
        }

        if (!PermissionNameRegex().IsMatch(trimmed))
        {
            return ValidationMessage;
        }

        return null;
    }

    [GeneratedRegex("^[a-z0-9._-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex PermissionNameRegex();
}
