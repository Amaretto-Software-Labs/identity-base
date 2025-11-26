using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace Identity.Base.OpenIddict.Handlers;

internal sealed class ClientCredentialsGrantHandler : IOpenIddictServerHandler<OpenIddictServerEvents.HandleTokenRequestContext>
{
    private readonly IOpenIddictScopeManager _scopeManager;

    public ClientCredentialsGrantHandler(IOpenIddictScopeManager scopeManager)
    {
        _scopeManager = scopeManager;
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

        var identity = new ClaimsIdentity(
            authenticationType: context.Options?.TokenValidationParameters?.AuthenticationType,
            nameType: OpenIddictConstants.Claims.Name,
            roleType: OpenIddictConstants.Claims.Role);

        var principal = new ClaimsPrincipal(identity);

        principal.SetClaim(OpenIddictConstants.Claims.Subject, context.ClientId);
        principal.SetClaim(OpenIddictConstants.Claims.ClientId, context.ClientId);

        var scopes = context.Request.GetScopes();
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

    public static OpenIddictServerHandlerDescriptor Descriptor { get; } =
        OpenIddictServerHandlerDescriptor.CreateBuilder<OpenIddictServerEvents.HandleTokenRequestContext>()
            .UseScopedHandler<ClientCredentialsGrantHandler>()
            .SetOrder(int.MinValue + 4500)
            .SetType(OpenIddictServerHandlerType.Custom)
            .Build();
}
