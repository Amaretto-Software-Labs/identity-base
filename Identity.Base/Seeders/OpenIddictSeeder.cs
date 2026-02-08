using Identity.Base.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;

namespace Identity.Base.Seeders;

internal sealed class OpenIddictSeeder(
    IOptions<OpenIddictOptions> openIddictOptions,
    IOpenIddictApplicationManager applicationManager,
    IOpenIddictScopeManager scopeManager,
    ILogger<OpenIddictSeeder> logger)
{
    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        await SeedApplicationsAsync(cancellationToken);
        await SeedScopesAsync(cancellationToken);
    }

    private async Task SeedApplicationsAsync(CancellationToken cancellationToken)
    {
        var configuredOptions = openIddictOptions.Value;

        foreach (var application in configuredOptions.Applications)
        {
            var descriptor = new OpenIddictApplicationDescriptor
            {
                ClientId = application.ClientId,
                ClientSecret = string.IsNullOrWhiteSpace(application.ClientSecret) ? null : application.ClientSecret,
                ClientType = application.ClientType
            };

            var permissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var uri in application.RedirectUris)
            {
                descriptor.RedirectUris.Add(new Uri(uri));
            }

            foreach (var uri in application.PostLogoutRedirectUris)
            {
                descriptor.PostLogoutRedirectUris.Add(new Uri(uri));
            }

            permissions.UnionWith(application.Permissions
                .Select(NormalizePermission)
                .Where(static permission => !string.IsNullOrWhiteSpace(permission)));

            if (application.AllowClientCredentialsFlow)
            {
                permissions.Add(OpenIddictConstants.Permissions.GrantTypes.ClientCredentials);
            }

            foreach (var permission in permissions)
            {
                descriptor.Permissions.Add(permission);
            }

            foreach (var requirement in application.Requirements)
            {
                var normalized = NormalizeRequirement(requirement);
                if (!string.IsNullOrWhiteSpace(normalized))
                {
                    descriptor.Requirements.Add(normalized);
                }
            }

            var existing = await applicationManager.FindByClientIdAsync(application.ClientId, cancellationToken);
            if (existing is null)
            {
                try
                {
                    await applicationManager.CreateAsync(descriptor, cancellationToken);
                    logger.LogInformation("Created OpenIddict application {ClientId}", application.ClientId);
                }
                catch (OpenIddictExceptions.ValidationException ex)
                {
                    existing = await applicationManager.FindByClientIdAsync(application.ClientId, cancellationToken);
                    if (existing is not null)
                    {
                        await applicationManager.UpdateAsync(existing, descriptor, cancellationToken);
                        logger.LogInformation("Updated OpenIddict application {ClientId} after duplicate detection", application.ClientId);
                    }
                    else
                    {
                        logger.LogError(ex, "Failed to create OpenIddict application {ClientId} due to validation errors.", application.ClientId);
                        throw;
                    }
                }
            }
            else
            {
                await applicationManager.UpdateAsync(existing, descriptor, cancellationToken);
                logger.LogInformation("Updated OpenIddict application {ClientId}", application.ClientId);
            }
        }
    }

    private static string NormalizePermission(string permission)
    {
        if (string.IsNullOrWhiteSpace(permission))
        {
            return permission;
        }

        var trimmed = permission.Trim();
        if (trimmed.StartsWith(OpenIddictConstants.Permissions.Prefixes.Scope, StringComparison.OrdinalIgnoreCase))
        {
            return trimmed;
        }

        if (trimmed.StartsWith(OpenIddictConstants.Permissions.Prefixes.Endpoint, StringComparison.OrdinalIgnoreCase))
        {
            return trimmed;
        }

        if (trimmed.StartsWith(OpenIddictConstants.Permissions.Prefixes.GrantType, StringComparison.OrdinalIgnoreCase))
        {
            return trimmed;
        }

        if (trimmed.StartsWith(OpenIddictConstants.Permissions.Prefixes.ResponseType, StringComparison.OrdinalIgnoreCase))
        {
            return trimmed;
        }

        if (trimmed.StartsWith(OpenIddictConstants.Permissions.Prefixes.Resource, StringComparison.OrdinalIgnoreCase))
        {
            return trimmed;
        }

        if (trimmed.StartsWith(OpenIddictConstants.Permissions.Prefixes.Audience, StringComparison.OrdinalIgnoreCase))
        {
            return trimmed;
        }

        if (trimmed.StartsWith("endpoints:", StringComparison.OrdinalIgnoreCase))
        {
            var remainder = trimmed["endpoints:".Length..].Trim();
            return string.IsNullOrWhiteSpace(remainder)
                ? string.Empty
                : OpenIddictConstants.Permissions.Prefixes.Endpoint + remainder;
        }

        if (trimmed.StartsWith("endpoint:", StringComparison.OrdinalIgnoreCase))
        {
            var remainder = trimmed["endpoint:".Length..].Trim();
            return string.IsNullOrWhiteSpace(remainder)
                ? string.Empty
                : OpenIddictConstants.Permissions.Prefixes.Endpoint + remainder;
        }

        if (trimmed.StartsWith("grant_types:", StringComparison.OrdinalIgnoreCase))
        {
            var remainder = trimmed["grant_types:".Length..].Trim();
            return string.IsNullOrWhiteSpace(remainder)
                ? string.Empty
                : OpenIddictConstants.Permissions.Prefixes.GrantType + remainder;
        }

        if (trimmed.StartsWith("grant_type:", StringComparison.OrdinalIgnoreCase))
        {
            var remainder = trimmed["grant_type:".Length..].Trim();
            return string.IsNullOrWhiteSpace(remainder)
                ? string.Empty
                : OpenIddictConstants.Permissions.Prefixes.GrantType + remainder;
        }

        if (trimmed.StartsWith("response_types:", StringComparison.OrdinalIgnoreCase))
        {
            var remainder = trimmed["response_types:".Length..].Trim();
            return string.IsNullOrWhiteSpace(remainder)
                ? string.Empty
                : OpenIddictConstants.Permissions.Prefixes.ResponseType + remainder;
        }

        if (trimmed.StartsWith("response_type:", StringComparison.OrdinalIgnoreCase))
        {
            var remainder = trimmed["response_type:".Length..].Trim();
            return string.IsNullOrWhiteSpace(remainder)
                ? string.Empty
                : OpenIddictConstants.Permissions.Prefixes.ResponseType + remainder;
        }

        if (trimmed.StartsWith("resources:", StringComparison.OrdinalIgnoreCase))
        {
            var remainder = trimmed["resources:".Length..].Trim();
            return string.IsNullOrWhiteSpace(remainder)
                ? string.Empty
                : OpenIddictConstants.Permissions.Prefixes.Resource + remainder;
        }

        if (trimmed.StartsWith("resource:", StringComparison.OrdinalIgnoreCase))
        {
            var remainder = trimmed["resource:".Length..].Trim();
            return string.IsNullOrWhiteSpace(remainder)
                ? string.Empty
                : OpenIddictConstants.Permissions.Prefixes.Resource + remainder;
        }

        if (trimmed.StartsWith("audiences:", StringComparison.OrdinalIgnoreCase))
        {
            var remainder = trimmed["audiences:".Length..].Trim();
            return string.IsNullOrWhiteSpace(remainder)
                ? string.Empty
                : OpenIddictConstants.Permissions.Prefixes.Audience + remainder;
        }

        if (trimmed.StartsWith("audience:", StringComparison.OrdinalIgnoreCase))
        {
            var remainder = trimmed["audience:".Length..].Trim();
            return string.IsNullOrWhiteSpace(remainder)
                ? string.Empty
                : OpenIddictConstants.Permissions.Prefixes.Audience + remainder;
        }

        if (trimmed.StartsWith("scopes:", StringComparison.OrdinalIgnoreCase))
        {
            var remainder = trimmed["scopes:".Length..].Trim();
            return string.IsNullOrWhiteSpace(remainder)
                ? string.Empty
                : OpenIddictConstants.Permissions.Prefixes.Scope + remainder;
        }

        if (trimmed.StartsWith("scope:", StringComparison.OrdinalIgnoreCase))
        {
            var remainder = trimmed["scope:".Length..].Trim();
            return string.IsNullOrWhiteSpace(remainder)
                ? string.Empty
                : OpenIddictConstants.Permissions.Prefixes.Scope + remainder;
        }

        return trimmed;
    }

    private static string NormalizeRequirement(string requirement)
    {
        if (string.IsNullOrWhiteSpace(requirement))
        {
            return requirement;
        }

        var trimmed = requirement.Trim();
        if (trimmed.StartsWith(OpenIddictConstants.Requirements.Prefixes.Feature, StringComparison.OrdinalIgnoreCase))
        {
            return trimmed;
        }

        if (trimmed.StartsWith("requirements:", StringComparison.OrdinalIgnoreCase))
        {
            var remainder = trimmed["requirements:".Length..].Trim();
            return string.IsNullOrWhiteSpace(remainder)
                ? string.Empty
                : OpenIddictConstants.Requirements.Prefixes.Feature + remainder;
        }

        if (trimmed.StartsWith("requirement:", StringComparison.OrdinalIgnoreCase))
        {
            var remainder = trimmed["requirement:".Length..].Trim();
            return string.IsNullOrWhiteSpace(remainder)
                ? string.Empty
                : OpenIddictConstants.Requirements.Prefixes.Feature + remainder;
        }

        return trimmed;
    }

    private async Task SeedScopesAsync(CancellationToken cancellationToken)
    {
        var configuredOptions = openIddictOptions.Value;

        foreach (var scope in configuredOptions.Scopes)
        {
            var descriptor = new OpenIddictScopeDescriptor
            {
                Name = scope.Name,
                DisplayName = scope.DisplayName
            };

            foreach (var resource in scope.Resources)
            {
                descriptor.Resources.Add(resource);
            }

            var existing = await scopeManager.FindByNameAsync(scope.Name, cancellationToken);
            if (existing is null)
            {
                try
                {
                    await scopeManager.CreateAsync(descriptor, cancellationToken);
                    logger.LogInformation("Created OpenIddict scope {Scope}", scope.Name);
                }
                catch (OpenIddictExceptions.ValidationException ex)
                {
                    existing = await scopeManager.FindByNameAsync(scope.Name, cancellationToken);
                    if (existing is not null)
                    {
                        await scopeManager.UpdateAsync(existing, descriptor, cancellationToken);
                        logger.LogInformation("Updated OpenIddict scope {Scope} after duplicate detection", scope.Name);
                    }
                    else
                    {
                        logger.LogError(ex, "Failed to create OpenIddict scope {Scope} due to validation errors.", scope.Name);
                        throw;
                    }
                }
            }
            else
            {
                await scopeManager.UpdateAsync(existing, descriptor, cancellationToken);
                logger.LogInformation("Updated OpenIddict scope {Scope}", scope.Name);
            }
        }
    }
}
