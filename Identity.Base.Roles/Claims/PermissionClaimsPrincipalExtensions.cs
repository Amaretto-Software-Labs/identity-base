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

        var permissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var value in EnumeratePermissions(principal))
        {
            permissions.Add(value);
        }

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
        foreach (var value in EnumeratePermissions(principal))
        {
            if (string.Equals(value, target, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
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

        foreach (var value in EnumeratePermissions(principal))
        {
            if (requested.Contains(value))
            {
                return true;
            }
        }

        return false;
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

        foreach (var value in EnumeratePermissions(principal))
        {
            requested.Remove(value);
            if (requested.Count == 0)
            {
                return true;
            }
        }

        return false;
    }

    private static HashSet<string> NormalizePermissions(IEnumerable<string> permissions)
    {
        var normalized = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var permission in permissions)
        {
            if (!string.IsNullOrWhiteSpace(permission))
            {
                normalized.Add(permission.Trim());
            }
        }

        return normalized;
    }

    private static IEnumerable<string> EnumeratePermissions(ClaimsPrincipal principal)
    {
        foreach (var claim in principal.FindAll(RoleClaimTypes.Permissions))
        {
            if (string.IsNullOrWhiteSpace(claim.Value))
            {
                continue;
            }

            var values = claim.Value.Split(Separator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var value in values)
            {
                yield return value;
            }
        }
    }
}
