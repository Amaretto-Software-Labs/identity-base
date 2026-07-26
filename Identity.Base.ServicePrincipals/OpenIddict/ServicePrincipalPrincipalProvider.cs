using System.Security.Claims;
using Identity.Base.OpenIddict;
using Identity.Base.Roles.Abstractions;
using Identity.Base.Roles.Claims;
using Identity.Base.ServicePrincipals.Data;
using Identity.Base.ServicePrincipals.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;

namespace Identity.Base.ServicePrincipals.OpenIddict;

internal sealed class ServicePrincipalPrincipalProvider(
    ServicePrincipalDbContext dbContext,
    IRoleDbContext roleDbContext,
    IOptions<ServicePrincipalOptions> options)
    : IClientCredentialsPrincipalProvider, IManagedClientCredentialsClientResolver
{
    public Task<bool> IsManagedAsync(string clientId, CancellationToken cancellationToken = default) =>
        dbContext.ServicePrincipals.AsNoTracking()
            .AnyAsync(item => item.ClientId == clientId && !item.IsDisabled, cancellationToken);

    public async Task<ClaimsPrincipal?> CreatePrincipalAsync(
        string clientId,
        IReadOnlyList<string> scopes,
        CancellationToken cancellationToken = default)
    {
        var servicePrincipal = await dbContext.ServicePrincipals.AsNoTracking()
            .SingleOrDefaultAsync(item => item.ClientId == clientId, cancellationToken);
        if (servicePrincipal is null)
        {
            return null;
        }
        if (servicePrincipal.IsDisabled)
        {
            throw new InvalidOperationException("A disabled service principal passed client authentication.");
        }

        var permissions = await roleDbContext.ServicePrincipalRoles
            .Where(item => item.ServicePrincipalId == servicePrincipal.Id)
            .Join(roleDbContext.RolePermissions, assignment => assignment.RoleId, item => item.RoleId,
                (_, item) => item.PermissionId)
            .Join(roleDbContext.Permissions, id => id, permission => permission.Id, (_, permission) => permission.Name)
            .Distinct()
            .ToListAsync(cancellationToken);

        var identity = new ClaimsIdentity("ServicePrincipal", OpenIddictConstants.Claims.Name, OpenIddictConstants.Claims.Role);
        var principal = new ClaimsPrincipal(identity);
        principal.SetClaim(OpenIddictConstants.Claims.Subject, servicePrincipal.Id.ToString("D"));
        principal.SetClaim(OpenIddictConstants.Claims.ClientId, servicePrincipal.ClientId);
        principal.SetClaim(OpenIddictConstants.Claims.Name, servicePrincipal.DisplayName);
        principal.SetClaim("identity.principal_type", "ServicePrincipal");
        var permissionClaim = string.Join(' ', permissions
            .Where(permission => !string.IsNullOrWhiteSpace(permission))
            .Select(permission => permission.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(permission => permission, StringComparer.OrdinalIgnoreCase));
        if (permissionClaim.Length > 0)
        {
            identity.AddClaim(new Claim(RoleClaimTypes.Permissions, permissionClaim));
        }
        principal.SetAccessTokenLifetime(options.Value.AccessTokenLifetime);
        return principal;
    }
}
