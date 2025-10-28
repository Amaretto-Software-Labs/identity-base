using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Security.Claims;
using Identity.Base.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using static OpenIddict.Server.AspNetCore.OpenIddictServerAspNetCoreConstants;

namespace Identity.Base.Features.Authentication.Authorize;

public static class AuthorizeEndpoint
{
    public static void MapAuthorizeEndpoint(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/connect/authorize", HandleAsync);
        endpoints.MapPost("/connect/authorize", HandleAsync);
    }

    private static async Task<IResult> HandleAsync(
        HttpContext context,
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        IOpenIddictScopeManager scopeManager,
        CancellationToken cancellationToken)
    {
        var request = Microsoft.AspNetCore.OpenIddictServerAspNetCoreHelpers.GetOpenIddictServerRequest(context);
        if (request is null)
        {
            throw new InvalidOperationException("Unable to retrieve the OpenID Connect request.");
        }

        if (request.HasPromptValue(OpenIddictConstants.PromptValues.Login))
        {
            await context.SignOutAsync(IdentityConstants.ApplicationScheme);
            return Results.Challenge(new AuthenticationProperties
            {
                RedirectUri = context.Request.PathBase + context.Request.Path + context.Request.QueryString
            }, new[] { IdentityConstants.ApplicationScheme });
        }

        var authenticateResult = await context.AuthenticateAsync(IdentityConstants.ApplicationScheme);
        if (!authenticateResult.Succeeded || authenticateResult.Principal is null)
        {
            if (request.HasPromptValue(OpenIddictConstants.PromptValues.None))
            {
                var properties = new AuthenticationProperties(new Dictionary<string, string?>
                {
                    [Properties.Error] = OpenIddictConstants.Errors.LoginRequired,
                    [Properties.ErrorDescription] = "User must sign in before continuing."
                });

                return Results.Forbid(properties, new[] { OpenIddictServerAspNetCoreDefaults.AuthenticationScheme });
            }

            return Results.Challenge(new AuthenticationProperties
            {
                RedirectUri = context.Request.PathBase + context.Request.Path + context.Request.QueryString
            }, new[] { IdentityConstants.ApplicationScheme });
        }

        var user = await userManager.GetUserAsync(authenticateResult.Principal);
        if (user is null)
        {
            await context.SignOutAsync(IdentityConstants.ApplicationScheme);
            return Results.Challenge(new AuthenticationProperties
            {
                RedirectUri = context.Request.PathBase + context.Request.Path + context.Request.QueryString
            }, new[] { IdentityConstants.ApplicationScheme });
        }

        context.User = authenticateResult.Principal;

        if (!await userManager.IsEmailConfirmedAsync(user))
        {
            return Results.Forbid(properties: null, authenticationSchemes: new[] { IdentityConstants.ApplicationScheme });
        }

        var principal = await signInManager.CreateUserPrincipalAsync(user);

        if (request.HasPromptValue(OpenIddictConstants.PromptValues.Consent))
        {
            principal.SetClaim("consent_granted", bool.TrueString);
        }

        principal.SetClaim(OpenIddictConstants.Claims.Subject, user.Id.ToString());

        if (!string.IsNullOrWhiteSpace(user.Email))
        {
            principal.SetClaim(OpenIddictConstants.Claims.Email, user.Email);
        }

        if (!string.IsNullOrWhiteSpace(user.DisplayName))
        {
            principal.SetClaim(OpenIddictConstants.Claims.Name, user.DisplayName);
        }

        foreach (var (key, value) in user.ProfileMetadata.Values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                principal.SetClaim($"metadata:{key}", value);
            }
        }

        var scopes = request.GetScopes();
        if (scopes.IsEmpty)
        {
            scopes = scopes.Add(OpenIddictConstants.Scopes.OpenId);
            scopes = scopes.Add(OpenIddictConstants.Scopes.Profile);
            scopes = scopes.Add(OpenIddictConstants.Scopes.Email);
        }

        principal.SetScopes(scopes);

        var resources = new HashSet<string>(StringComparer.Ordinal);
        await foreach (var resource in scopeManager.ListResourcesAsync(principal.GetScopes(), cancellationToken))
        {
            resources.Add(resource);
        }

        if (resources.Count > 0)
        {
            principal.SetResources(resources);
        }

        principal.SetDestinations(GetDestinations);

        return Results.SignIn(principal, properties: null, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    private static IEnumerable<string> GetDestinations(Claim claim)
    {
        return claim.Type switch
        {
            OpenIddictConstants.Claims.Subject => new[]
            {
                OpenIddictConstants.Destinations.AccessToken,
                OpenIddictConstants.Destinations.IdentityToken
            },
            OpenIddictConstants.Claims.Email => new[]
            {
                OpenIddictConstants.Destinations.AccessToken,
                OpenIddictConstants.Destinations.IdentityToken
            },
            OpenIddictConstants.Claims.Name => new[]
            {
                OpenIddictConstants.Destinations.AccessToken,
                OpenIddictConstants.Destinations.IdentityToken
            },
            _ when claim.Type.StartsWith("metadata:", StringComparison.OrdinalIgnoreCase) => new[]
            {
                OpenIddictConstants.Destinations.AccessToken
            },
            _ => new[] { OpenIddictConstants.Destinations.AccessToken }
        };
    }
}
