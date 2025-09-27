using System.Collections.Generic;
using System.Security.Claims;
using Identity.Base.Identity;
using Identity.Base.Abstractions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace Identity.Base.OpenIddict.Handlers;

internal sealed class PasswordGrantHandler : IOpenIddictServerHandler<OpenIddictServerEvents.HandleTokenRequestContext>
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IOpenIddictScopeManager _scopeManager;
    private readonly ILogger<PasswordGrantHandler> _logger;
    private readonly IEnumerable<IClaimsPrincipalAugmentor> _augmentors;

    public PasswordGrantHandler(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        IOpenIddictScopeManager scopeManager,
        ILogger<PasswordGrantHandler> logger,
        IEnumerable<IClaimsPrincipalAugmentor> augmentors)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _scopeManager = scopeManager;
        _logger = logger;
        _augmentors = augmentors;
    }

    public async ValueTask HandleAsync(OpenIddictServerEvents.HandleTokenRequestContext context)
    {
        if (!context.Request.IsPasswordGrantType())
        {
            return;
        }

        var email = context.Request.Username ?? string.Empty;
        var user = await _userManager.FindByEmailAsync(email) ?? await _userManager.FindByNameAsync(email);

        if (user is null)
        {
            context.Reject(OpenIddictConstants.Errors.InvalidGrant, "Invalid credentials.");
            return;
        }

        var password = context.Request.Password ?? string.Empty;
        var signInResult = await _signInManager.CheckPasswordSignInAsync(user, password, lockoutOnFailure: true);

        if (signInResult.IsLockedOut)
        {
            context.Reject(OpenIddictConstants.Errors.InvalidGrant, "User account is locked out.");
            return;
        }

        if (!signInResult.Succeeded)
        {
            context.Reject(OpenIddictConstants.Errors.InvalidGrant, "Invalid credentials.");
            return;
        }

        if (!_userManager.SupportsUserEmail || !await _userManager.IsEmailConfirmedAsync(user))
        {
            context.Reject(OpenIddictConstants.Errors.AccessDenied, "Email must be confirmed before logging in.");
            return;
        }

        var principal = await _signInManager.CreateUserPrincipalAsync(user);

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

        var scopes = (context.Request.Scope ?? string.Empty)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (scopes.Length == 0)
        {
            scopes = new[]
            {
                OpenIddictConstants.Scopes.OpenId,
                OpenIddictConstants.Scopes.Profile,
                OpenIddictConstants.Scopes.Email,
                OpenIddictConstants.Scopes.OfflineAccess
            };
        }

        principal.SetScopes(scopes);

        var resources = new HashSet<string>(StringComparer.Ordinal);
        await foreach (var resource in _scopeManager.ListResourcesAsync(principal.GetScopes(), context.CancellationToken))
        {
            resources.Add(resource);
        }

        if (resources.Count > 0)
        {
            principal.SetResources(resources);
        }

        foreach (var augmentor in _augmentors)
        {
            await augmentor.AugmentAsync(user, principal, context.CancellationToken).ConfigureAwait(false);
        }

        principal.SetDestinations(GetDestinations);

        context.Principal = principal;
        _logger.LogInformation("Password grant succeeded for user {UserId}", user.Id);
    }

    private static IEnumerable<string> GetDestinations(Claim claim)
    {
        switch (claim.Type)
        {
            case OpenIddictConstants.Claims.Subject:
            case OpenIddictConstants.Claims.Email:
            case OpenIddictConstants.Claims.Name:
                yield return OpenIddictConstants.Destinations.AccessToken;
                yield return OpenIddictConstants.Destinations.IdentityToken;
                yield break;
            default:
                if (claim.Type.StartsWith("metadata:", StringComparison.OrdinalIgnoreCase))
                {
                    yield return OpenIddictConstants.Destinations.AccessToken;
                    yield break;
                }

                yield return OpenIddictConstants.Destinations.AccessToken;
                yield break;
        }
    }

    public static OpenIddictServerHandlerDescriptor Descriptor { get; } =
        OpenIddictServerHandlerDescriptor.CreateBuilder<OpenIddictServerEvents.HandleTokenRequestContext>()
            .UseScopedHandler<PasswordGrantHandler>()
            .SetOrder(int.MinValue + 5000)
            .SetType(OpenIddictServerHandlerType.Custom)
            .Build();
}
