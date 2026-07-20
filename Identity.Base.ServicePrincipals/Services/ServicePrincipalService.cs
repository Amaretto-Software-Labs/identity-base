using System.Security.Cryptography;
using Identity.Base.Roles.Abstractions;
using Identity.Base.Roles.Entities;
using Identity.Base.ServicePrincipals.Data;
using Identity.Base.ServicePrincipals.Domain;
using Identity.Base.ServicePrincipals.Options;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;

namespace Identity.Base.ServicePrincipals.Services;

public sealed class ServicePrincipalService(
    ServicePrincipalDbContext dbContext,
    IRoleDbContext roleDbContext,
    IOpenIddictApplicationManager applicationManager,
    IOpenIddictTokenManager tokenManager,
    IPasswordHasher<ServicePrincipalCredential> passwordHasher,
    IOptions<ServicePrincipalOptions> options,
    IEnumerable<IServicePrincipalLifecycleListener> lifecycleListeners)
{
    public async Task<ServicePrincipal> CreateAsync(string displayName, string clientId, CancellationToken cancellationToken)
    {
        var principal = new ServicePrincipal(displayName.Trim(), clientId.Trim());
        if (await dbContext.ServicePrincipals.AnyAsync(item => item.ClientId == principal.ClientId, cancellationToken))
        {
            throw new InvalidOperationException("Client ID already exists.");
        }

        if (await applicationManager.FindByClientIdAsync(principal.ClientId, cancellationToken) is not null)
        {
            throw new InvalidOperationException("Client ID is already registered with OpenIddict.");
        }

        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = principal.ClientId,
            // OpenIddict requires confidential applications to own a secret. Managed
            // credentials are validated by ServicePrincipalClientSecretValidator;
            // this random sentinel is hashed by OpenIddict and never disclosed.
            ClientSecret = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32)),
            DisplayName = principal.DisplayName,
            ClientType = OpenIddictConstants.ClientTypes.Confidential
        };
        descriptor.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.Token);
        descriptor.Permissions.Add(OpenIddictConstants.Permissions.GrantTypes.ClientCredentials);
        foreach (var scope in options.Value.AllowedScopes.Where(scope => !string.IsNullOrWhiteSpace(scope)))
        {
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.Prefixes.Scope + scope.Trim());
        }
        descriptor.SetAccessTokenLifetime(options.Value.AccessTokenLifetime);

        await applicationManager.CreateAsync(descriptor, cancellationToken);
        dbContext.ServicePrincipals.Add(principal);
        await dbContext.SaveChangesAsync(cancellationToken);
        return principal;
    }

    public async Task<IssuedCredential> IssueCredentialAsync(
        Guid servicePrincipalId,
        string name,
        DateTimeOffset? expiresAt,
        CancellationToken cancellationToken)
    {
        var principal = await FindRequiredAsync(servicePrincipalId, cancellationToken);
        if (principal.IsDisabled)
        {
            throw new InvalidOperationException("Disabled service principals cannot receive credentials.");
        }

        var normalizedName = name.Trim();
        if (await dbContext.ServicePrincipalCredentials.AnyAsync(
            item => item.ServicePrincipalId == servicePrincipalId && item.Name == normalizedName,
            cancellationToken))
        {
            throw new InvalidOperationException("Credential name already exists.");
        }

        var secret = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        var credential = new ServicePrincipalCredential(servicePrincipalId, normalizedName, string.Empty, expiresAt);
        var hash = passwordHasher.HashPassword(credential, secret);
        credential = new ServicePrincipalCredential(servicePrincipalId, normalizedName, hash, expiresAt);
        dbContext.ServicePrincipalCredentials.Add(credential);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new IssuedCredential(credential, secret);
    }

    public async Task<bool> ValidateCredentialAsync(string clientId, string secret, CancellationToken cancellationToken)
    {
        var principal = await dbContext.ServicePrincipals
            .Include(item => item.Credentials)
            .SingleOrDefaultAsync(item => item.ClientId == clientId, cancellationToken);
        if (principal is null || principal.IsDisabled)
        {
            return false;
        }

        var now = DateTimeOffset.UtcNow;
        return principal.Credentials
            .Where(item => item.IsActive(now))
            .Any(item => passwordHasher.VerifyHashedPassword(item, item.SecretHash, secret)
                is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded);
    }

    public async Task DisableAsync(Guid id, string? reason, CancellationToken cancellationToken)
    {
        var principal = await FindRequiredWithCredentialsAsync(id, cancellationToken);
        foreach (var listener in lifecycleListeners)
        {
            await listener.BeforeDisableAsync(id, cancellationToken);
        }
        principal.Disable();
        foreach (var credential in principal.Credentials)
        {
            credential.Revoke(reason ?? "Service principal disabled.");
        }
        await dbContext.SaveChangesAsync(cancellationToken);
        await RevokeTokensAsync(principal.Id, cancellationToken);
    }

    public async Task RestoreAsync(Guid id, CancellationToken cancellationToken)
    {
        var principal = await FindRequiredAsync(id, cancellationToken);
        principal.Restore();
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RevokeCredentialAsync(
        Guid servicePrincipalId,
        Guid credentialId,
        string? reason,
        CancellationToken cancellationToken)
    {
        var credential = await dbContext.ServicePrincipalCredentials.SingleOrDefaultAsync(
            item => item.ServicePrincipalId == servicePrincipalId && item.Id == credentialId,
            cancellationToken) ?? throw new KeyNotFoundException("Credential was not found.");
        credential.Revoke(reason);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RevokeAllCredentialsAsync(Guid servicePrincipalId, string? reason, CancellationToken cancellationToken)
    {
        var principal = await FindRequiredWithCredentialsAsync(servicePrincipalId, cancellationToken);
        foreach (var credential in principal.Credentials)
        {
            credential.Revoke(reason);
        }
        await dbContext.SaveChangesAsync(cancellationToken);
        await RevokeTokensAsync(principal.Id, cancellationToken);
    }

    public async Task ReplaceRolesAsync(Guid id, IReadOnlyCollection<string> roleNames, CancellationToken cancellationToken)
    {
        _ = await FindRequiredAsync(id, cancellationToken);
        var normalized = roleNames.Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var roles = await roleDbContext.Roles.Where(role => normalized.Contains(role.Name)).ToListAsync(cancellationToken);
        if (roles.Count != normalized.Length)
        {
            throw new InvalidOperationException("One or more roles do not exist.");
        }

        var existing = await roleDbContext.ServicePrincipalRoles
            .Where(item => item.ServicePrincipalId == id).ToListAsync(cancellationToken);
        roleDbContext.ServicePrincipalRoles.RemoveRange(existing);
        roleDbContext.ServicePrincipalRoles.AddRange(roles.Select(role => new ServicePrincipalRole
        {
            ServicePrincipalId = id,
            RoleId = role.Id
        }));
        await roleDbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<ServicePrincipal> FindRequiredAsync(Guid id, CancellationToken cancellationToken) =>
        await dbContext.ServicePrincipals.SingleOrDefaultAsync(item => item.Id == id, cancellationToken)
        ?? throw new KeyNotFoundException("Service principal was not found.");

    private async Task<ServicePrincipal> FindRequiredWithCredentialsAsync(Guid id, CancellationToken cancellationToken) =>
        await dbContext.ServicePrincipals.Include(item => item.Credentials)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken)
        ?? throw new KeyNotFoundException("Service principal was not found.");

    private async Task RevokeTokensAsync(Guid principalId, CancellationToken cancellationToken)
    {
        await foreach (var token in tokenManager.FindBySubjectAsync(
            principalId.ToString("D"),
            cancellationToken))
        {
            _ = await tokenManager.TryRevokeAsync(token, cancellationToken);
        }
    }
}

public sealed record IssuedCredential(ServicePrincipalCredential Credential, string Secret);
