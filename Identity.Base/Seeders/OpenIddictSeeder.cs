using System.Linq;
using Identity.Base.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;

namespace Identity.Base.Seeders;

internal sealed class OpenIddictSeeder
{
    private readonly IOptions<OpenIddictOptions> _options;
    private readonly IOpenIddictApplicationManager _applicationManager;
    private readonly IOpenIddictScopeManager _scopeManager;
    private readonly ILogger<OpenIddictSeeder> _logger;

    public OpenIddictSeeder(
        IOptions<OpenIddictOptions> options,
        IOpenIddictApplicationManager applicationManager,
        IOpenIddictScopeManager scopeManager,
        ILogger<OpenIddictSeeder> logger)
    {
        _options = options;
        _applicationManager = applicationManager;
        _scopeManager = scopeManager;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        await SeedApplicationsAsync(cancellationToken);
        await SeedScopesAsync(cancellationToken);
    }

    private async Task SeedApplicationsAsync(CancellationToken cancellationToken)
    {
        var options = _options.Value;

        foreach (var application in options.Applications)
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

            foreach (var permission in application.Permissions)
            {
                var normalized = NormalizePermission(permission);
                if (!string.IsNullOrWhiteSpace(normalized))
                {
                    permissions.Add(normalized);
                }
            }

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
                descriptor.Requirements.Add(requirement);
            }

            var existing = await _applicationManager.FindByClientIdAsync(application.ClientId, cancellationToken);
            if (existing is null)
            {
                try
                {
                    await _applicationManager.CreateAsync(descriptor, cancellationToken);
                    _logger.LogInformation("Created OpenIddict application {ClientId}", application.ClientId);
                }
                catch (OpenIddictExceptions.ValidationException)
                {
                    existing = await _applicationManager.FindByClientIdAsync(application.ClientId, cancellationToken);
                    if (existing is not null)
                    {
                        await _applicationManager.UpdateAsync(existing, descriptor, cancellationToken);
                        _logger.LogInformation("Updated OpenIddict application {ClientId} after duplicate detection", application.ClientId);
                    }
                }
            }
            else
            {
                await _applicationManager.UpdateAsync(existing, descriptor, cancellationToken);
                _logger.LogInformation("Updated OpenIddict application {ClientId}", application.ClientId);
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

        if (trimmed.StartsWith("scopes:", StringComparison.OrdinalIgnoreCase))
        {
            return OpenIddictConstants.Permissions.Prefixes.Scope + trimmed["scopes:".Length..];
        }

        if (trimmed.StartsWith("scope:", StringComparison.OrdinalIgnoreCase))
        {
            return OpenIddictConstants.Permissions.Prefixes.Scope + trimmed["scope:".Length..];
        }

        return trimmed;
    }

    private async Task SeedScopesAsync(CancellationToken cancellationToken)
    {
        var options = _options.Value;

        foreach (var scope in options.Scopes)
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

            var existing = await _scopeManager.FindByNameAsync(scope.Name, cancellationToken);
            if (existing is null)
            {
                try
                {
                    await _scopeManager.CreateAsync(descriptor, cancellationToken);
                    _logger.LogInformation("Created OpenIddict scope {Scope}", scope.Name);
                }
                catch (OpenIddictExceptions.ValidationException)
                {
                    existing = await _scopeManager.FindByNameAsync(scope.Name, cancellationToken);
                    if (existing is not null)
                    {
                        await _scopeManager.UpdateAsync(existing, descriptor, cancellationToken);
                        _logger.LogInformation("Updated OpenIddict scope {Scope} after duplicate detection", scope.Name);
                    }
                }
            }
            else
            {
                await _scopeManager.UpdateAsync(existing, descriptor, cancellationToken);
                _logger.LogInformation("Updated OpenIddict scope {Scope}", scope.Name);
            }
        }
    }
}
