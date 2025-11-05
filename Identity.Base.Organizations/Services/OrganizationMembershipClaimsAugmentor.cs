using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Identity.Base.Abstractions;
using Identity.Base.Identity;
using Identity.Base.Organizations.Claims;
using Identity.Base.Organizations.Data;
using Identity.Base.Organizations.Domain;
using Identity.Base.Roles.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Identity.Base.Organizations.Services;

public sealed class OrganizationMembershipClaimsAugmentor : IClaimsPrincipalAugmentor
{
    private readonly OrganizationDbContext _organizationDbContext;

    public OrganizationMembershipClaimsAugmentor(OrganizationDbContext organizationDbContext)
    {
        _organizationDbContext = organizationDbContext ?? throw new ArgumentNullException(nameof(organizationDbContext));
    }

    public async Task AugmentAsync(ApplicationUser user, ClaimsPrincipal principal, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(principal);

        var identity = principal.Identities.OfType<ClaimsIdentity>().FirstOrDefault();
        if (identity is null)
        {
            return;
        }

        var memberships = await _organizationDbContext.OrganizationMemberships
            .AsNoTracking()
            .Where(membership => membership.UserId == user.Id)
            .Select(membership => membership.OrganizationId)
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (memberships.Count == 0)
        {
            return;
        }

        var claimValue = string.Join(' ', memberships.Select(id => id.ToString("D")));
        var existing = identity.FindFirst(OrganizationClaimTypes.OrganizationMemberships);
        if (existing is not null)
        {
            identity.RemoveClaim(existing);
        }

        identity.AddClaim(new Claim(OrganizationClaimTypes.OrganizationMemberships, claimValue));
    }
}
