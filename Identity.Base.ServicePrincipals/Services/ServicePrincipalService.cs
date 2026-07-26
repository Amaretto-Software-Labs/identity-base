using System.Security.Cryptography;
using System.Text.RegularExpressions;
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
    public async Task<ServicePrincipal> CreateAsync(string displayName, CancellationToken cancellationToken)
    {
        var normalizedDisplayName = ServicePrincipal.NormalizeDisplayName(displayName);
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var clientId = GenerateClientId(normalizedDisplayName);
            if (await dbContext.ServicePrincipals.AnyAsync(item => item.ClientId == clientId, cancellationToken)
                || await applicationManager.FindByClientIdAsync(clientId, cancellationToken) is not null)
            {
                continue;
            }

            return await CreateAsync(normalizedDisplayName, clientId, cancellationToken);
        }

        throw new InvalidOperationException("Unable to generate a unique client ID.");
    }

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

        var application = await applicationManager.CreateAsync(descriptor, cancellationToken);
        try
        {
            dbContext.ServicePrincipals.Add(principal);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            dbContext.Entry(principal).State = EntityState.Detached;
            await applicationManager.DeleteAsync(application, CancellationToken.None);
            throw;
        }
        return principal;
    }

    public async Task<IssuedCredential> IssueCredentialAsync(
        Guid servicePrincipalId,
        string name,
        DateTimeOffset? expiresAt,
        CancellationToken cancellationToken)
    {
        var normalizedName = ServicePrincipalCredential.NormalizeName(name);
        if (expiresAt is { } expiry && expiry <= DateTimeOffset.UtcNow)
        {
            throw new ArgumentException("Credential expiry must be in the future.", nameof(expiresAt));
        }

        var principal = await FindRequiredAsync(servicePrincipalId, cancellationToken);
        if (principal.IsDisabled)
        {
            throw new InvalidOperationException("Disabled service principals cannot receive credentials.");
        }

        if (await dbContext.ServicePrincipalCredentials.AnyAsync(
            item => item.ServicePrincipalId == servicePrincipalId && item.Name == normalizedName,
            cancellationToken))
        {
            throw new InvalidOperationException("Credential name already exists.");
        }

        var secret = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        var credential = new ServicePrincipalCredential(servicePrincipalId, normalizedName, string.Empty, expiresAt);
        var hash = passwordHasher.HashPassword(credential, secret);
        credential.SetSecretHash(hash);
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
        foreach (var credential in principal.Credentials.Where(item => item.IsActive(now)))
        {
            var result = passwordHasher.VerifyHashedPassword(credential, credential.SecretHash, secret);
            if (result == PasswordVerificationResult.Failed)
            {
                continue;
            }

            if (result == PasswordVerificationResult.SuccessRehashNeeded)
            {
                credential.SetSecretHash(passwordHasher.HashPassword(credential, secret));
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            return true;
        }

        return false;
    }

    public async Task DisableAsync(Guid id, string? reason, CancellationToken cancellationToken)
    {
        var revokedReason = ServicePrincipalCredential.NormalizeRevokedReason(
            reason ?? "Service principal disabled.");
        var principal = await FindRequiredWithCredentialsAsync(id, cancellationToken);
        foreach (var listener in lifecycleListeners)
        {
            await listener.BeforeDisableAsync(id, cancellationToken);
        }
        principal.Disable();
        foreach (var credential in principal.Credentials)
        {
            credential.Revoke(revokedReason);
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
        var revokedReason = ServicePrincipalCredential.NormalizeRevokedReason(reason);
        var credential = await dbContext.ServicePrincipalCredentials.SingleOrDefaultAsync(
            item => item.ServicePrincipalId == servicePrincipalId && item.Id == credentialId,
            cancellationToken) ?? throw new KeyNotFoundException("Credential was not found.");
        credential.Revoke(revokedReason);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RevokeAllCredentialsAsync(Guid servicePrincipalId, string? reason, CancellationToken cancellationToken)
    {
        var revokedReason = ServicePrincipalCredential.NormalizeRevokedReason(reason);
        var principal = await FindRequiredWithCredentialsAsync(servicePrincipalId, cancellationToken);
        foreach (var credential in principal.Credentials)
        {
            credential.Revoke(revokedReason);
        }
        await dbContext.SaveChangesAsync(cancellationToken);
        await RevokeTokensAsync(principal.Id, cancellationToken);
    }

    public async Task ReplaceRolesAsync(Guid id, IReadOnlyCollection<string> roleNames, CancellationToken cancellationToken)
    {
        _ = await FindRequiredAsync(id, cancellationToken);
        var normalized = roleNames.Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var normalizedKeys = normalized.Select(name => name.ToUpperInvariant()).ToArray();
        var candidates = roleDbContext.Database.ProviderName?.Contains(
                "InMemory",
                StringComparison.OrdinalIgnoreCase) == true
            ? roleDbContext.Roles.AsEnumerable()
                .Where(role => normalized.Contains(role.Name, StringComparer.OrdinalIgnoreCase))
                .ToList()
            : await roleDbContext.Roles
                .Where(role => normalizedKeys.Contains(role.Name.ToUpper()))
                .ToListAsync(cancellationToken);
        var rolesByName = candidates.ToLookup(role => role.Name, StringComparer.OrdinalIgnoreCase);
        if (normalized.Any(name => rolesByName[name].Count() != 1))
        {
            throw new InvalidOperationException("One or more roles do not exist or are ambiguous.");
        }
        var roles = normalized.Select(name => rolesByName[name].Single()).ToArray();

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

    private static string GenerateClientId(string displayName)
    {
        var prefix = Regex.Replace(
                displayName.Trim().ToLowerInvariant(),
                "[^a-z0-9]+",
                "-",
                RegexOptions.CultureInvariant)
            .Trim('-');
        if (string.IsNullOrWhiteSpace(prefix))
        {
            prefix = "service-principal";
        }

        prefix = prefix[..Math.Min(prefix.Length, 180)];
        var suffix = Convert.ToHexString(RandomNumberGenerator.GetBytes(6)).ToLowerInvariant();
        return $"{prefix}-{suffix}";
    }
}

public sealed record IssuedCredential(ServicePrincipalCredential Credential, string Secret);
