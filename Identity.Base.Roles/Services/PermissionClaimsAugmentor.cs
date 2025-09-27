using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Identity.Base.Abstractions;
using Identity.Base.Identity;
using Identity.Base.Roles.Claims;

namespace Identity.Base.Roles.Services;

public sealed class PermissionClaimsAugmentor : IClaimsPrincipalAugmentor
{
    private readonly IPermissionResolver _permissionResolver;

    public PermissionClaimsAugmentor(IPermissionResolver permissionResolver)
    {
        _permissionResolver = permissionResolver;
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
            .SelectMany(claim => claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        var union = new HashSet<string>(existingPermissions, StringComparer.OrdinalIgnoreCase);
        foreach (var permission in permissions)
        {
            if (!string.IsNullOrWhiteSpace(permission))
            {
                union.Add(permission.Trim());
            }
        }

        if (union.Count == 0)
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

        var value = string.Join(' ', union.OrderBy(permission => permission, StringComparer.OrdinalIgnoreCase));
        identity.AddClaim(new Claim(RoleClaimTypes.Permissions, value));
    }
}
