using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Identity.Base.OpenIddict;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace Identity.Base.OpenIddict.Handlers;

internal sealed class ClientCredentialsGrantHandler : IOpenIddictServerHandler<OpenIddictServerEvents.HandleTokenRequestContext>
{
    private readonly IOpenIddictScopeManager _scopeManager;
    private readonly IEnumerable<IClientCredentialsPrincipalProvider> _principalProviders;

    public ClientCredentialsGrantHandler(
        IOpenIddictScopeManager scopeManager,
        IEnumerable<IClientCredentialsPrincipalProvider> principalProviders)
    {
        _scopeManager = scopeManager;
        _principalProviders = principalProviders;
    }

    public async ValueTask HandleAsync(OpenIddictServerEvents.HandleTokenRequestContext context)
    {
        if (!context.Request.IsClientCredentialsGrantType())
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(context.ClientId))
        {
            context.Reject(OpenIddictConstants.Errors.InvalidClient, "Client credentials are required.");
            return;
        }

        var scopes = context.Request.GetScopes();
        ClaimsPrincipal principal;
        try
        {
            principal = await CreateManagedPrincipalAsync(context.ClientId, scopes, context.CancellationToken)
                ?? CreateLegacyPrincipal(context, context.ClientId);
        }
        catch (InvalidOperationException)
        {
            context.Reject(OpenIddictConstants.Errors.InvalidClient, "Invalid client credentials.");
            return;
        }
        principal.SetScopes(scopes);

        if (!scopes.IsEmpty)
        {
            var resources = new HashSet<string>(StringComparer.Ordinal);
            await foreach (var resource in _scopeManager.ListResourcesAsync(principal.GetScopes(), context.CancellationToken))
            {
                resources.Add(resource);
            }

            if (resources.Count > 0)
            {
                principal.SetResources(resources);
            }
        }

        principal.SetDestinations(static _ => new[] { OpenIddictConstants.Destinations.AccessToken });

        context.Principal = principal;
    }

    private async Task<ClaimsPrincipal?> CreateManagedPrincipalAsync(
        string clientId,
        IReadOnlyList<string> scopes,
        CancellationToken cancellationToken)
    {
        foreach (var provider in _principalProviders)
        {
            var principal = await provider.CreatePrincipalAsync(clientId, scopes, cancellationToken);
            if (principal is not null)
            {
                return principal;
            }
        }

        return null;
    }

    private static ClaimsPrincipal CreateLegacyPrincipal(
        OpenIddictServerEvents.HandleTokenRequestContext context,
        string clientId)
    {
        var identity = new ClaimsIdentity(
            authenticationType: context.Options?.TokenValidationParameters?.AuthenticationType,
            nameType: OpenIddictConstants.Claims.Name,
            roleType: OpenIddictConstants.Claims.Role);
        var principal = new ClaimsPrincipal(identity);
        principal.SetClaim(OpenIddictConstants.Claims.Subject, clientId);
        principal.SetClaim(OpenIddictConstants.Claims.ClientId, clientId);
        return principal;
    }

    public static OpenIddictServerHandlerDescriptor Descriptor { get; } =
        OpenIddictServerHandlerDescriptor.CreateBuilder<OpenIddictServerEvents.HandleTokenRequestContext>()
            .UseScopedHandler<ClientCredentialsGrantHandler>()
            .SetOrder(int.MinValue + 4500)
            .SetType(OpenIddictServerHandlerType.Custom)
            .Build();
}
