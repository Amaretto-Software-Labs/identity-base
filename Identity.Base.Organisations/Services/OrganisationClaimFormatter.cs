using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using Identity.Base.Abstractions.MultiTenancy;
using Identity.Base.Identity;
using Identity.Base.Organisations.Abstractions;
using Identity.Base.Organisations.Claims;
using Identity.Base.Roles.Abstractions;
using Identity.Base.Roles.Claims;

namespace Identity.Base.Organisations.Services;

public sealed class OrganisationClaimFormatter : IPermissionClaimFormatter
{
    private readonly IOrganisationContextAccessor _organisationContextAccessor;

    public OrganisationClaimFormatter(IOrganisationContextAccessor organisationContextAccessor)
    {
        _organisationContextAccessor = organisationContextAccessor ?? throw new ArgumentNullException(nameof(organisationContextAccessor));
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

        var organisationContext = _organisationContextAccessor.Current;
        if (organisationContext.HasOrganisation)
        {
            if (organisationContext.OrganisationId.HasValue)
            {
                claims.Add(new Claim(OrganisationClaimTypes.OrganisationId, organisationContext.OrganisationId.Value.ToString("D")));
            }

            if (!string.IsNullOrWhiteSpace(organisationContext.OrganisationSlug))
            {
                claims.Add(new Claim(OrganisationClaimTypes.OrganisationSlug, organisationContext.OrganisationSlug));
            }

            if (!string.IsNullOrWhiteSpace(organisationContext.DisplayName))
            {
                claims.Add(new Claim(OrganisationClaimTypes.OrganisationDisplayName, organisationContext.DisplayName));
            }
        }

        return claims.Count == 0 ? Array.Empty<Claim>() : claims;
    }
}
