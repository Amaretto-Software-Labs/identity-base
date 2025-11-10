using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Identity.Base.Abstractions;
using Identity.Base.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace Identity.Base.OpenIddict.Handlers;

internal sealed class RefreshTokenAugmentorHandler : IOpenIddictServerHandler<OpenIddictServerEvents.HandleTokenRequestContext>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<RefreshTokenAugmentorHandler> _logger;
    private readonly IEnumerable<IClaimsPrincipalAugmentor> _augmentors;

    public RefreshTokenAugmentorHandler(
        UserManager<ApplicationUser> userManager,
        ILogger<RefreshTokenAugmentorHandler> logger,
        IEnumerable<IClaimsPrincipalAugmentor> augmentors)
    {
        _userManager = userManager;
        _logger = logger;
        _augmentors = augmentors;
    }

    public async ValueTask HandleAsync(OpenIddictServerEvents.HandleTokenRequestContext context)
    {
        if (!context.Request.IsRefreshTokenGrantType())
        {
            return;
        }

        var principal = context.Principal;
        if (principal is null)
        {
            _logger.LogDebug("Skipping refresh augmentation because principal is null.");
            return;
        }

        var subject = principal.GetClaim(OpenIddictConstants.Claims.Subject);
        if (string.IsNullOrWhiteSpace(subject) || !Guid.TryParse(subject, out _))
        {
            _logger.LogDebug("Skipping refresh augmentation because subject claim is missing or invalid.");
            return;
        }

        var user = await _userManager.FindByIdAsync(subject).ConfigureAwait(false);
        if (user is null)
        {
            _logger.LogDebug("Skipping refresh augmentation because user {Subject} could not be found.", subject);
            return;
        }

        foreach (var augmentor in _augmentors)
        {
            await augmentor.AugmentAsync(user, principal, context.CancellationToken).ConfigureAwait(false);
        }

        principal.SetDestinations(PasswordGrantHandler.GetDestinations);
        context.Principal = principal;
    }

    public static OpenIddictServerHandlerDescriptor Descriptor { get; } =
        OpenIddictServerHandlerDescriptor.CreateBuilder<OpenIddictServerEvents.HandleTokenRequestContext>()
            .UseScopedHandler<RefreshTokenAugmentorHandler>()
            .SetOrder(PasswordGrantHandler.Descriptor.Order + 20)
            .SetType(OpenIddictServerHandlerType.Custom)
            .Build();
}

