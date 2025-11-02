using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using Identity.Base.Abstractions.MultiTenancy;
using Identity.Base.Identity;
using Identity.Base.Organizations.Abstractions;
using Identity.Base.Organizations.Claims;
using Identity.Base.Roles.Abstractions;
using Identity.Base.Roles.Claims;

namespace Identity.Base.Organizations.Services;

public sealed class OrganizationClaimFormatter : IPermissionClaimFormatter
{
    private readonly IOrganizationContextAccessor _organizationContextAccessor;

    public OrganizationClaimFormatter(IOrganizationContextAccessor organizationContextAccessor)
    {
        _organizationContextAccessor = organizationContextAccessor ?? throw new ArgumentNullException(nameof(organizationContextAccessor));
    }

    public IReadOnlyCollection<Claim> CreateClaims(ApplicationUser user, IReadOnlyCollection<string> permissions, ITenantContext tenantContext)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(permissions);
        ArgumentNullException.ThrowIfNull(tenantContext);

        var claims = new List<Claim>();

        var orderedPermissions = permissions
            .Where(static permission => !string.IsNullOrWhiteSpace(permission))
            .Select(static permission => permission.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static permission => permission, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (orderedPermissions.Length > 0)
        {
            claims.Add(new Claim(RoleClaimTypes.Permissions, string.Join(' ', orderedPermissions)));
        }

        var organizationContext = _organizationContextAccessor.Current;
        if (organizationContext.HasOrganization)
        {
            if (organizationContext.OrganizationId.HasValue)
            {
                claims.Add(new Claim(OrganizationClaimTypes.OrganizationId, organizationContext.OrganizationId.Value.ToString("D")));
            }

            if (!string.IsNullOrWhiteSpace(organizationContext.OrganizationSlug))
            {
                claims.Add(new Claim(OrganizationClaimTypes.OrganizationSlug, organizationContext.OrganizationSlug));
            }

            if (!string.IsNullOrWhiteSpace(organizationContext.DisplayName))
            {
                claims.Add(new Claim(OrganizationClaimTypes.OrganizationDisplayName, organizationContext.DisplayName));
            }
        }

        return claims.Count == 0 ? Array.Empty<Claim>() : claims;
    }
}
