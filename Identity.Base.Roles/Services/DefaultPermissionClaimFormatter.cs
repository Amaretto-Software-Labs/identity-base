using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using Identity.Base.Abstractions.MultiTenancy;
using Identity.Base.Identity;
using Identity.Base.Roles.Abstractions;
using Identity.Base.Roles.Claims;

namespace Identity.Base.Roles.Services;

/// <summary>
/// Default formatter that emits a single space-delimited permissions claim.
/// </summary>
public sealed class DefaultPermissionClaimFormatter : IPermissionClaimFormatter
{
    public IReadOnlyCollection<Claim> CreateClaims(ApplicationUser user, IReadOnlyCollection<string> permissions, ITenantContext tenantContext)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(permissions);
        ArgumentNullException.ThrowIfNull(tenantContext);

        if (permissions.Count == 0)
        {
            return Array.Empty<Claim>();
        }

        var ordered = permissions
            .Where(static permission => !string.IsNullOrWhiteSpace(permission))
            .Select(static permission => permission.Trim())
            .OrderBy(static permission => permission, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (ordered.Length == 0)
        {
            return Array.Empty<Claim>();
        }

        return new[] { new Claim(RoleClaimTypes.Permissions, string.Join(' ', ordered)) };
    }
}
