using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Identity.Base.Abstractions;
using Identity.Base.Abstractions.MultiTenancy;
using Identity.Base.Identity;
using Identity.Base.Roles.Abstractions;
using Identity.Base.Roles.Claims;

namespace Identity.Base.Roles.Services;

public sealed class PermissionClaimsAugmentor : IClaimsPrincipalAugmentor
{
    private readonly IPermissionResolver _permissionResolver;
    private readonly ITenantContextAccessor _tenantContextAccessor;
    private readonly IPermissionClaimFormatter _claimFormatter;

    public PermissionClaimsAugmentor(
        IPermissionResolver permissionResolver,
        ITenantContextAccessor tenantContextAccessor,
        IPermissionClaimFormatter claimFormatter)
    {
        _permissionResolver = permissionResolver;
        _tenantContextAccessor = tenantContextAccessor;
        _claimFormatter = claimFormatter;
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

        var permissions = await _permissionResolver
            .GetEffectivePermissionsAsync(user.Id, cancellationToken)
            .ConfigureAwait(false);

        if (permissions.Count == 0)
        {
            return;
        }

        var existingPermissions = principal
            .FindAll(RoleClaimTypes.Permissions)
            .SelectMany(static claim => claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        var permissionSet = new HashSet<string>(existingPermissions, StringComparer.OrdinalIgnoreCase);
        foreach (var permission in permissions)
        {
            if (!string.IsNullOrWhiteSpace(permission))
            {
                permissionSet.Add(permission.Trim());
            }
        }

        if (permissionSet.Count == 0)
        {
            return;
        }

        foreach (var claim in principal.FindAll(RoleClaimTypes.Permissions).ToList())
        {
            if (claim.Subject is ClaimsIdentity claimIdentity)
            {
                claimIdentity.RemoveClaim(claim);
            }
        }

        var formattedClaims = _claimFormatter.CreateClaims(user, permissionSet.ToArray(), _tenantContextAccessor.Current);
        foreach (var claim in formattedClaims)
        {
            identity.AddClaim(claim);
        }
    }
}
