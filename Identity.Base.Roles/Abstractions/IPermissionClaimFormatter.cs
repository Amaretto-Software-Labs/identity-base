using System.Collections.Generic;
using System.Security.Claims;
using Identity.Base.Abstractions.MultiTenancy;
using Identity.Base.Identity;

namespace Identity.Base.Roles.Abstractions;

/// <summary>
/// Formats permission values into claims added to authenticated principals.
/// </summary>
public interface IPermissionClaimFormatter
{
    /// <summary>
    /// Creates claims representing the supplied permissions for the specified user and tenant context.
    /// </summary>
    /// <param name="user">The authenticated user.</param>
    /// <param name="permissions">The permissions granted to the user.</param>
    /// <param name="tenantContext">The active tenant context.</param>
    /// <returns>A collection of claims to append to the identity.</returns>
    IReadOnlyCollection<Claim> CreateClaims(ApplicationUser user, IReadOnlyCollection<string> permissions, ITenantContext tenantContext);
}
