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

        var memberships = SplitValues(claimValue)
            .Select(value => Guid.TryParse(value, out var membership) ? membership : (Guid?)null)
            .Where(value => value.HasValue)
            .Select(value => value!.Value)
            .Distinct()
            .ToArray();

        return memberships.Length == 0 ? Array.Empty<Guid>() : memberships;
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
        return SplitValues(claimValue)
            .Any(value => string.Equals(value, target, StringComparison.OrdinalIgnoreCase));
    }

    private static IEnumerable<string> SplitValues(string value)
    {
        return value.Split(Separator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}
