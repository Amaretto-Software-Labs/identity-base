using System.Security.Claims;

namespace Identity.Base.Roles.Claims;

public static class PermissionClaimsPrincipalExtensions
{
    private const char Separator = ' ';

    public static IReadOnlyCollection<string> GetPermissions(this ClaimsPrincipal principal)
    {
        if (principal is null)
        {
            throw new ArgumentNullException(nameof(principal));
        }

        var permissions = EnumeratePermissions(principal).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return permissions.Count == 0 ? Array.Empty<string>() : permissions.ToArray();
    }

    public static bool HasPermission(this ClaimsPrincipal principal, string permission)
    {
        if (principal is null)
        {
            throw new ArgumentNullException(nameof(principal));
        }

        if (string.IsNullOrWhiteSpace(permission))
        {
            return false;
        }

        var target = permission.Trim();
        return EnumeratePermissions(principal)
            .Any(value => string.Equals(value, target, StringComparison.OrdinalIgnoreCase));
    }

    public static bool HasAnyPermission(this ClaimsPrincipal principal, IEnumerable<string> permissions)
    {
        if (principal is null)
        {
            throw new ArgumentNullException(nameof(principal));
        }

        if (permissions is null)
        {
            throw new ArgumentNullException(nameof(permissions));
        }

        var requested = NormalizePermissions(permissions);
        if (requested.Count == 0)
        {
            return false;
        }

        return EnumeratePermissions(principal).Any(requested.Contains);
    }

    public static bool HasAllPermissions(this ClaimsPrincipal principal, IEnumerable<string> permissions)
    {
        if (principal is null)
        {
            throw new ArgumentNullException(nameof(principal));
        }

        if (permissions is null)
        {
            throw new ArgumentNullException(nameof(permissions));
        }

        var requested = NormalizePermissions(permissions);
        if (requested.Count == 0)
        {
            return false;
        }

        requested.ExceptWith(EnumeratePermissions(principal));
        return requested.Count == 0;
    }

    private static HashSet<string> NormalizePermissions(IEnumerable<string> permissions)
    {
        return permissions
            .Where(permission => !string.IsNullOrWhiteSpace(permission))
            .Select(permission => permission.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> EnumeratePermissions(ClaimsPrincipal principal)
    {
        return principal
            .FindAll(RoleClaimTypes.Permissions)
            .SelectMany(claim => SplitValues(claim.Value));
    }

    private static IEnumerable<string> SplitValues(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Array.Empty<string>();
        }

        return value.Split(Separator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}
