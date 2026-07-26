using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Identity.Base.Abstractions;
using Identity.Base.Identity;
using Identity.Base.OpenIddict;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace Identity.Base.OpenIddict.Handlers;

internal sealed class RefreshTokenAugmentorHandler : IOpenIddictServerHandler<OpenIddictServerEvents.ProcessSignInContext>
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

    public async ValueTask HandleAsync(OpenIddictServerEvents.ProcessSignInContext context)
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
            _logger.LogInformation("Rejecting refresh token because user {Subject} could not be found.", subject);
            context.Reject(OpenIddictConstants.Errors.InvalidGrant, "The refresh token is no longer valid.");
            return;
        }

        if (await _userManager.IsLockedOutAsync(user).ConfigureAwait(false))
        {
            _logger.LogInformation("Rejecting refresh token because user {Subject} is locked out.", subject);
            context.Reject(OpenIddictConstants.Errors.InvalidGrant, "The refresh token is no longer valid.");
            return;
        }

        var securityStampClaimType = _userManager.Options.ClaimsIdentity.SecurityStampClaimType;
        var tokenSecurityStamp = principal.FindFirstValue(securityStampClaimType);
        var currentSecurityStamp = await _userManager.GetSecurityStampAsync(user).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(tokenSecurityStamp)
            || !string.Equals(tokenSecurityStamp, currentSecurityStamp, StringComparison.Ordinal))
        {
            _logger.LogInformation("Rejecting refresh token because the security stamp changed for user {Subject}.", subject);
            context.Reject(OpenIddictConstants.Errors.InvalidGrant, "The refresh token is no longer valid.");
            return;
        }

        foreach (var augmentor in _augmentors)
        {
            await augmentor.AugmentAsync(user, principal, context.CancellationToken).ConfigureAwait(false);
        }

        principal.SetDestinations(OpenIddictClaimDestinations.GetDestinations);
    }

    public static OpenIddictServerHandlerDescriptor Descriptor { get; } =
        OpenIddictServerHandlerDescriptor.CreateBuilder<OpenIddictServerEvents.ProcessSignInContext>()
            .UseScopedHandler<RefreshTokenAugmentorHandler>()
            .SetOrder(int.MinValue + 5020)
            .SetType(OpenIddictServerHandlerType.Custom)
            .Build();
}
