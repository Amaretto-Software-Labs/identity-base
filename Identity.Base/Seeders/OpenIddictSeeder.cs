using Identity.Base.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;

namespace Identity.Base.Seeders;

public sealed class OpenIddictSeeder
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
                descriptor.Permissions.Add(permission);
            }

            if (application.AllowPasswordFlow)
            {
                descriptor.Permissions.Add(OpenIddictConstants.Permissions.GrantTypes.Password);
            }

            descriptor.Permissions.Add(OpenIddictConstants.Permissions.Prefixes.Scope + "identity.api");

            descriptor.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.Authorization);
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.Token);
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.Introspection);
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode);
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.GrantTypes.RefreshToken);
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.ResponseTypes.Code);
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.Prefixes.Scope + OpenIddictConstants.Scopes.OpenId);
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.Prefixes.Scope + OpenIddictConstants.Scopes.Profile);
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.Prefixes.Scope + OpenIddictConstants.Scopes.Email);
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.Prefixes.Scope + OpenIddictConstants.Scopes.OfflineAccess);

            foreach (var requirement in application.Requirements)
            {
                descriptor.Requirements.Add(requirement);
            }

            descriptor.Requirements.Add(OpenIddictConstants.Requirements.Features.ProofKeyForCodeExchange);

            var existing = await _applicationManager.FindByClientIdAsync(application.ClientId, cancellationToken);
            if (existing is null)
            {
                await _applicationManager.CreateAsync(descriptor, cancellationToken);
                _logger.LogInformation("Created OpenIddict application {ClientId}", application.ClientId);
            }
            else
            {
                await _applicationManager.UpdateAsync(existing, descriptor, cancellationToken);
                _logger.LogInformation("Updated OpenIddict application {ClientId}", application.ClientId);
            }
        }
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
                await _scopeManager.CreateAsync(descriptor, cancellationToken);
                _logger.LogInformation("Created OpenIddict scope {Scope}", scope.Name);
            }
            else
            {
                await _scopeManager.UpdateAsync(existing, descriptor, cancellationToken);
                _logger.LogInformation("Updated OpenIddict scope {Scope}", scope.Name);
            }
        }
    }
}
