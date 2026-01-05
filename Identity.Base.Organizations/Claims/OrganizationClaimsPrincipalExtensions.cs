using System.Security.Claims;

namespace Identity.Base.Organizations.Claims;

public static class OrganizationClaimsPrincipalExtensions
{
    private const char Separator = ' ';

    public static Guid? GetOrganizationId(this ClaimsPrincipal principal)
    {
        if (principal is null)
        {
            throw new ArgumentNullException(nameof(principal));
        }

        var claimValue = principal.FindFirstValue(OrganizationClaimTypes.OrganizationId);
        return Guid.TryParse(claimValue, out var organizationId) ? organizationId : null;
    }

    public static IReadOnlyCollection<Guid> GetOrganizationMemberships(this ClaimsPrincipal principal)
    {
        if (principal is null)
        {
            throw new ArgumentNullException(nameof(principal));
        }

        var claimValue = principal.FindFirstValue(OrganizationClaimTypes.OrganizationMemberships);
        if (string.IsNullOrWhiteSpace(claimValue))
        {
            return Array.Empty<Guid>();
        }

        var memberships = new HashSet<Guid>();
        foreach (var value in SplitValues(claimValue))
        {
            if (Guid.TryParse(value, out var membership))
            {
                memberships.Add(membership);
            }
        }

        return memberships.Count == 0 ? Array.Empty<Guid>() : memberships.ToArray();
    }

    public static bool HasOrganizationMembership(this ClaimsPrincipal principal, Guid organizationId)
    {
        if (principal is null)
        {
            throw new ArgumentNullException(nameof(principal));
        }

        if (organizationId == Guid.Empty)
        {
            return false;
        }

        var claimValue = principal.FindFirstValue(OrganizationClaimTypes.OrganizationMemberships);
        if (string.IsNullOrWhiteSpace(claimValue))
        {
            return false;
        }

        var target = organizationId.ToString("D");
        foreach (var value in SplitValues(claimValue))
        {
            if (string.Equals(value, target, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<string> SplitValues(string value)
    {
        return value.Split(Separator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}
