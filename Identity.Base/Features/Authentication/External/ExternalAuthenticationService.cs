using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using Identity.Base.Identity;
using Identity.Base.Logging;
using Identity.Base.Options;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.MicrosoftAccount;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Identity.Base.Features.Authentication.External;

public sealed class ExternalAuthenticationService
{
    private readonly IOptions<ExternalProviderOptions> _providerOptions;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<ExternalAuthenticationService> _logger;
    private readonly IAuditLogger _auditLogger;

    public ExternalAuthenticationService(
        IOptions<ExternalProviderOptions> providerOptions,
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        ILogger<ExternalAuthenticationService> logger,
        IAuditLogger auditLogger)
    {
        _providerOptions = providerOptions;
        _signInManager = signInManager;
        _userManager = userManager;
        _logger = logger;
        _auditLogger = auditLogger;
    }

    public async Task<IResult> StartAsync(HttpContext httpContext, string provider, string? returnUrl, string mode, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var (scheme, settings) = ResolveProvider(provider);
        if (scheme is null || settings is null)
        {
            return Results.Problem($"Unknown external provider '{provider}'.", statusCode: StatusCodes.Status400BadRequest);
        }

        if (!settings.Enabled)
        {
            return Results.Problem($"External provider '{provider}' is disabled.", statusCode: StatusCodes.Status400BadRequest);
        }

        if (!string.IsNullOrWhiteSpace(returnUrl) && !IsRelativeUrl(returnUrl))
        {
            return Results.Problem("returnUrl must be a relative path.", statusCode: StatusCodes.Status400BadRequest);
        }

        var normalizedMode = string.Equals(mode, ExternalAuthenticationConstants.ModeLink, StringComparison.OrdinalIgnoreCase)
            ? ExternalAuthenticationConstants.ModeLink
            : ExternalAuthenticationConstants.ModeLogin;

        string? userId = null;
        if (normalizedMode == ExternalAuthenticationConstants.ModeLink)
        {
            var authenticateResult = await httpContext.AuthenticateAsync(IdentityConstants.ApplicationScheme);
            if (!authenticateResult.Succeeded || authenticateResult.Principal is null)
            {
                return Results.Unauthorized();
            }

            httpContext.User = authenticateResult.Principal;

            var user = await _userManager.GetUserAsync(authenticateResult.Principal);
            if (user is null)
            {
                return Results.Unauthorized();
            }

            userId = user.Id.ToString();
        }

        var callbackUri = BuildCallbackUri(httpContext, provider);
        var properties = _signInManager.ConfigureExternalAuthenticationProperties(scheme, callbackUri, userId);
        properties.Items[ExternalAuthenticationConstants.ModeKey] = normalizedMode;
        if (!string.IsNullOrWhiteSpace(returnUrl))
        {
            properties.Items[ExternalAuthenticationConstants.ReturnUrlKey] = returnUrl;
        }

        if (!string.IsNullOrEmpty(userId))
        {
            properties.Items[ExternalAuthenticationConstants.LinkUserIdKey] = userId;
        }

        await httpContext.SignOutAsync(IdentityConstants.ExternalScheme);

        return Results.Challenge(properties, new[] { scheme });
    }

    public async Task<IResult> HandleCallbackAsync(HttpContext httpContext, string provider, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var (scheme, settings) = ResolveProvider(provider);
        if (scheme is null || settings is null)
        {
            await httpContext.SignOutAsync(IdentityConstants.ExternalScheme);
            return Results.Problem($"Unknown external provider '{provider}'.", statusCode: StatusCodes.Status400BadRequest);
        }

        if (!settings.Enabled)
        {
            await httpContext.SignOutAsync(IdentityConstants.ExternalScheme);
            return Results.Problem($"External provider '{provider}' is disabled.", statusCode: StatusCodes.Status400BadRequest);
        }

        ApplicationUser? currentUser = null;
        if (httpContext.User.Identity?.IsAuthenticated == true)
        {
            currentUser = await _userManager.GetUserAsync(httpContext.User);
        }

        var currentUserId = currentUser is null ? null : currentUser.Id.ToString();
        ExternalLoginInfo? info = await _signInManager.GetExternalLoginInfoAsync(currentUserId);
        if (info is null)
        {
            await httpContext.SignOutAsync(IdentityConstants.ExternalScheme);
            return Results.Problem("Unable to load external login information.", statusCode: StatusCodes.Status400BadRequest);
        }

        var mode = info.AuthenticationProperties?.Items.TryGetValue(ExternalAuthenticationConstants.ModeKey, out var modeValue) == true
            ? modeValue
            : ExternalAuthenticationConstants.ModeLogin;

        var returnUrl = info.AuthenticationProperties?.Items.TryGetValue(ExternalAuthenticationConstants.ReturnUrlKey, out var storedReturnUrl) == true
            ? storedReturnUrl
            : null;

        if (!string.IsNullOrWhiteSpace(returnUrl) && !IsRelativeUrl(returnUrl))
        {
            returnUrl = null;
        }

        if (string.Equals(mode, ExternalAuthenticationConstants.ModeLink, StringComparison.OrdinalIgnoreCase))
        {
            var linkUserId = info.AuthenticationProperties?.Items.TryGetValue(ExternalAuthenticationConstants.LinkUserIdKey, out var storedUserId) == true
                ? storedUserId
                : null;

            if (currentUser is null && !string.IsNullOrWhiteSpace(linkUserId))
            {
                currentUser = await _userManager.FindByIdAsync(linkUserId);
            }

            if (currentUser is null)
            {
                var authenticateResult = await httpContext.AuthenticateAsync(IdentityConstants.ApplicationScheme);
                if (authenticateResult.Succeeded && authenticateResult.Principal is not null)
                {
                    currentUser = await _userManager.GetUserAsync(authenticateResult.Principal);
                    httpContext.User = authenticateResult.Principal;
                }
            }

            return await HandleLinkAsync(httpContext, currentUser, info, returnUrl, cancellationToken);
        }

        return await HandleSignInAsync(httpContext, info, returnUrl, cancellationToken);
    }

    public async Task<IResult> UnlinkAsync(HttpContext httpContext, string provider, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var (_, settings) = ResolveProvider(provider);
        if (settings is null)
        {
            return Results.Problem($"Unknown external provider '{provider}'.", statusCode: StatusCodes.Status400BadRequest);
        }

        var user = await _userManager.GetUserAsync(httpContext.User);
        if (user is null)
        {
            return Results.Unauthorized();
        }

        var logins = await _userManager.GetLoginsAsync(user);
        var login = logins.FirstOrDefault(login => string.Equals(login.LoginProvider, settings.ProviderName, StringComparison.OrdinalIgnoreCase));
        if (login is null)
        {
            return Results.Problem($"External provider '{provider}' is not linked to this account.", statusCode: StatusCodes.Status400BadRequest);
        }

        var result = await _userManager.RemoveLoginAsync(user, login.LoginProvider, login.ProviderKey);
        if (!result.Succeeded)
        {
            return Results.Problem("Failed to unlink external provider.", statusCode: StatusCodes.Status400BadRequest);
        }

        await _signInManager.RefreshSignInAsync(user);
        await _auditLogger.LogAsync(AuditEventTypes.ExternalUnlinked, user.Id, new { Provider = login.LoginProvider }, cancellationToken);
        return Results.Ok(new { message = $"Provider '{provider}' unlinked." });
    }

    private async Task<IResult> HandleLinkAsync(HttpContext httpContext, ApplicationUser? currentUser, ExternalLoginInfo info, string? returnUrl, CancellationToken cancellationToken)
    {
        if (currentUser is null)
        {
            await httpContext.SignOutAsync(IdentityConstants.ExternalScheme);
            return Results.Unauthorized();
        }

        var addLoginResult = await _userManager.AddLoginAsync(currentUser, info);
        await httpContext.SignOutAsync(IdentityConstants.ExternalScheme);

        if (!addLoginResult.Succeeded)
        {
            var description = string.Join(", ", addLoginResult.Errors.Select(error => error.Description));
            _logger.LogWarning("Failed to link provider {Provider} for user {UserId}: {Errors}", info.LoginProvider, currentUser.Id, description);
            return CreateLinkResponse(returnUrl, "error", description);
        }

        await _signInManager.RefreshSignInAsync(currentUser);
        _logger.LogInformation("Linked provider {Provider} for user {UserId}", info.LoginProvider, currentUser.Id);
        await _auditLogger.LogAsync(AuditEventTypes.ExternalLinked, currentUser.Id, new { Provider = info.LoginProvider }, cancellationToken);
        return CreateLinkResponse(returnUrl, "linked", null);
    }

    private async Task<IResult> HandleSignInAsync(HttpContext httpContext, ExternalLoginInfo info, string? returnUrl, CancellationToken cancellationToken)
    {
        var signInResult = await _signInManager.ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey, isPersistent: false, bypassTwoFactor: false);
        if (signInResult.Succeeded)
        {
            await httpContext.SignOutAsync(IdentityConstants.ExternalScheme);
            _logger.LogInformation("User signed in with {Provider}", info.LoginProvider);
            var existing = await _userManager.FindByLoginAsync(info.LoginProvider, info.ProviderKey);
            if (existing is not null)
            {
                await _auditLogger.LogAsync(AuditEventTypes.ExternalLogin, existing.Id, new { Provider = info.LoginProvider }, cancellationToken);
            }
            return CreateLoginResponse(returnUrl, "success", null, requiresTwoFactor: false, methods: null);
        }

        if (signInResult.RequiresTwoFactor)
        {
            await httpContext.SignOutAsync(IdentityConstants.ExternalScheme);
            var twoFactorUser = await _signInManager.GetTwoFactorAuthenticationUserAsync();
            if (twoFactorUser is not null)
            {
                var providers = await _userManager.GetValidTwoFactorProvidersAsync(twoFactorUser);
                var methods = providers
                    .Select(MapTwoFactorProvider)
                    .Where(static method => method is not null)
                    .Cast<string>()
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Append("recovery")
                    .ToList();

                return CreateLoginResponse(returnUrl, "requiresTwoFactor", null, requiresTwoFactor: true, methods: methods);
            }

            return CreateLoginResponse(returnUrl, "requiresTwoFactor", null, requiresTwoFactor: true, methods: new List<string> { "authenticator" });
        }

        if (signInResult.IsLockedOut)
        {
            await httpContext.SignOutAsync(IdentityConstants.ExternalScheme);
            return CreateLoginResponse(returnUrl, "locked", "User account is locked out.", requiresTwoFactor: false, methods: null);
        }

        // Attempt to link or create a user based on external login information.
        var user = await FindOrCreateUserFromExternalLoginAsync(info, cancellationToken);
        if (user is null)
        {
            await httpContext.SignOutAsync(IdentityConstants.ExternalScheme);
            return CreateLoginResponse(returnUrl, "error", "Unable to create or locate user for external login.", requiresTwoFactor: false, methods: null);
        }

        var addLoginResult = await _userManager.AddLoginAsync(user, info);
        if (!addLoginResult.Succeeded)
        {
            await httpContext.SignOutAsync(IdentityConstants.ExternalScheme);
            var description = string.Join(", ", addLoginResult.Errors.Select(error => error.Description));
            _logger.LogWarning("Failed to associate external login {Provider} for user {UserId}: {Errors}", info.LoginProvider, user.Id, description);
            return CreateLoginResponse(returnUrl, "error", "Unable to associate external login.", requiresTwoFactor: false, methods: null);
        }

        await _signInManager.SignInAsync(user, isPersistent: false);
        await httpContext.SignOutAsync(IdentityConstants.ExternalScheme);
        _logger.LogInformation("User {UserId} created via {Provider} external login", user.Id, info.LoginProvider);
        await _auditLogger.LogAsync(AuditEventTypes.ExternalLogin, user.Id, new { Provider = info.LoginProvider, Created = true }, cancellationToken);
        return CreateLoginResponse(returnUrl, "success", null, requiresTwoFactor: false, methods: null);
    }

    private async Task<ApplicationUser?> FindOrCreateUserFromExternalLoginAsync(ExternalLoginInfo info, CancellationToken cancellationToken)
    {
        var email = info.Principal.FindFirstValue(ClaimTypes.Email);
        if (!string.IsNullOrWhiteSpace(email))
        {
            var existingByEmail = await _userManager.FindByEmailAsync(email);
            if (existingByEmail is not null)
            {
                return existingByEmail;
            }
        }

        var userName = email ?? $"{info.LoginProvider}_{info.ProviderKey}";
        var displayName = info.Principal.FindFirstValue(ClaimTypes.Name) ?? userName;

        var user = new ApplicationUser
        {
            UserName = userName,
            Email = email,
            EmailConfirmed = !string.IsNullOrWhiteSpace(email),
            DisplayName = displayName
        };

        var createResult = await _userManager.CreateAsync(user);
        if (!createResult.Succeeded)
        {
            var description = string.Join(", ", createResult.Errors.Select(error => error.Description));
            _logger.LogWarning("Failed to create user for external login {Provider}: {Errors}", info.LoginProvider, description);
            return null;
        }

        if (!string.IsNullOrWhiteSpace(email) && !await _userManager.IsEmailConfirmedAsync(user))
        {
            user.EmailConfirmed = true;
            await _userManager.UpdateAsync(user);
        }

        return user;
    }

    private static string BuildCallbackUri(HttpContext context, string provider)
    {
        var request = context.Request;
        var callbackPath = $"/auth/external/{provider}/callback";
        return new Uri(new Uri($"{request.Scheme}://{request.Host}"), callbackPath).ToString();
    }

    private static bool IsRelativeUrl(string url)
        => Uri.TryCreate(url, UriKind.Relative, out _);

    private static string? MapTwoFactorProvider(string provider)
    {
        if (string.Equals(provider, TokenOptions.DefaultAuthenticatorProvider, StringComparison.OrdinalIgnoreCase))
        {
            return "authenticator";
        }

        if (string.Equals(provider, TokenOptions.DefaultPhoneProvider, StringComparison.OrdinalIgnoreCase))
        {
            return "sms";
        }

        if (string.Equals(provider, TokenOptions.DefaultEmailProvider, StringComparison.OrdinalIgnoreCase))
        {
            return "email";
        }

        return null;
    }

    private (string? Scheme, ProviderDescriptor? Settings) ResolveProvider(string provider)
    {
        var options = _providerOptions.Value;
        var normalized = provider?.Trim().ToLowerInvariant();
        return normalized switch
        {
            "google" => (GoogleDefaults.AuthenticationScheme, new ProviderDescriptor("Google", options.Google)),
            "microsoft" => (MicrosoftAccountDefaults.AuthenticationScheme, new ProviderDescriptor("Microsoft", options.Microsoft)),
            "apple" => (ExternalAuthenticationConstants.AppleScheme, new ProviderDescriptor("Apple", options.Apple)),
            _ => (null, null)
        };
    }

    private IResult CreateLinkResponse(string? returnUrl, string status, string? message)
    {
        if (!string.IsNullOrWhiteSpace(returnUrl))
        {
            var redirected = QueryHelpers.AddQueryString(returnUrl, new Dictionary<string, string?>
            {
                ["status"] = status,
                ["message"] = message
            });
            return Results.Redirect(redirected);
        }

        return Results.Ok(new { status, message });
    }

    private IResult CreateLoginResponse(string? returnUrl, string status, string? message, bool requiresTwoFactor, IReadOnlyCollection<string>? methods)
    {
        if (!string.IsNullOrWhiteSpace(returnUrl))
        {
            var query = new Dictionary<string, string?>
            {
                ["status"] = status,
                ["message"] = message,
                ["requiresTwoFactor"] = requiresTwoFactor ? "true" : "false"
            };

            if (methods is not null && methods.Count > 0)
            {
                query["methods"] = string.Join(',', methods);
            }

            var redirected = QueryHelpers.AddQueryString(returnUrl, query);
            return Results.Redirect(redirected);
        }

        return Results.Ok(new
        {
            status,
            message,
            requiresTwoFactor,
            methods
        });
    }

    private sealed record ProviderDescriptor(string ProviderName, OAuthProviderOptions Options)
    {
        public bool Enabled => Options.Enabled;
    }
}
