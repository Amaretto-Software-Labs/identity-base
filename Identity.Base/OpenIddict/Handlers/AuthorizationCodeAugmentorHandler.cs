using System.Collections.Generic;
using System.Threading.Tasks;
using Identity.Base.Abstractions;
using Identity.Base.Identity;
using Identity.Base.OpenIddict;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace Identity.Base.OpenIddict.Handlers;

internal sealed class AuthorizationCodeAugmentorHandler : IOpenIddictServerHandler<OpenIddictServerEvents.ProcessSignInContext>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<AuthorizationCodeAugmentorHandler> _logger;
    private readonly IEnumerable<IClaimsPrincipalAugmentor> _augmentors;

    public AuthorizationCodeAugmentorHandler(
        UserManager<ApplicationUser> userManager,
        ILogger<AuthorizationCodeAugmentorHandler> logger,
        IEnumerable<IClaimsPrincipalAugmentor> augmentors)
    {
        _userManager = userManager;
        _logger = logger;
        _augmentors = augmentors;
    }

    public async ValueTask HandleAsync(OpenIddictServerEvents.ProcessSignInContext context)
    {
        if (context.Principal is null)
        {
            return;
        }

        if (!context.Request.IsAuthorizationCodeFlow() && !context.Request.IsHybridFlow())
        {
            return;
        }

        var subject = context.Principal.GetClaim(OpenIddictConstants.Claims.Subject);
        if (string.IsNullOrWhiteSpace(subject) || !Guid.TryParse(subject, out _))
        {
            _logger.LogDebug("Skipping permission augmentation because subject claim is missing or invalid.");
            return;
        }

        var user = await _userManager.FindByIdAsync(subject).ConfigureAwait(false);
        if (user is null)
        {
            _logger.LogDebug("Skipping permission augmentation because user {Subject} could not be found.", subject);
            return;
        }

        foreach (var augmentor in _augmentors)
        {
            await augmentor.AugmentAsync(user, context.Principal, context.CancellationToken).ConfigureAwait(false);
        }

        context.Principal.SetDestinations(OpenIddictClaimDestinations.GetDestinations);
    }

    public static OpenIddictServerHandlerDescriptor Descriptor { get; } =
        OpenIddictServerHandlerDescriptor.CreateBuilder<OpenIddictServerEvents.ProcessSignInContext>()
            .UseScopedHandler<AuthorizationCodeAugmentorHandler>()
            .SetOrder(int.MinValue + 5010)
            .SetType(OpenIddictServerHandlerType.Custom)
            .Build();
}
