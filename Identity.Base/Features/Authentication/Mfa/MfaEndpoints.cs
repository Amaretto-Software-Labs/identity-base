using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FluentValidation;
using Identity.Base.Extensions;
using Identity.Base.Identity;
using Identity.Base.Logging;
using Identity.Base.Options;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Identity.Base.Features.Authentication.Mfa;

public static class MfaEndpoints
{
    public static RouteGroupBuilder MapMfaEndpoints(this RouteGroupBuilder group)
    {
        var mfaGroup = group.MapGroup("/mfa");

        mfaGroup.MapPost("/enroll", EnrollAsync)
            .RequireAuthorization(new AuthorizeAttribute { AuthenticationSchemes = IdentityConstants.ApplicationScheme })
            .WithName("EnrollMfa")
            .WithSummary("Starts authenticator app enrollment and returns the shared key and otpauth URI.")
            .Produces(StatusCodes.Status200OK)
            .WithTags("Authentication");

        mfaGroup.MapPost("/verify", VerifyAsync)
            .WithName("VerifyMfa")
            .WithSummary("Verifies an authenticator code. Works for both enrollment and login step-up.")
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithTags("Authentication");

        mfaGroup.MapPost("/challenge", ChallengeAsync)
            .WithName("SendMfaChallenge")
            .WithSummary("Sends an MFA challenge via the selected method (e.g., SMS).")
            .Produces(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithTags("Authentication");

        mfaGroup.MapPost("/disable", DisableAsync)
            .RequireAuthorization(new AuthorizeAttribute { AuthenticationSchemes = IdentityConstants.ApplicationScheme })
            .WithName("DisableMfa")
            .WithSummary("Disables authenticator MFA for the current user.")
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithTags("Authentication");

        mfaGroup.MapPost("/recovery-codes", RegenerateRecoveryCodesAsync)
            .RequireAuthorization(new AuthorizeAttribute { AuthenticationSchemes = IdentityConstants.ApplicationScheme })
            .WithName("RegenerateRecoveryCodes")
            .WithSummary("Generates new recovery codes for the current user.")
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithTags("Authentication");

        return group;
    }

    private static async Task<IResult> EnrollAsync(
        HttpContext context,
        UserManager<ApplicationUser> userManager,
        IOptions<MfaOptions> mfaOptions,
        IAuditLogger auditLogger,
        CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(context.User);
        if (user is null)
        {
            return Results.Unauthorized();
        }

        await userManager.ResetAuthenticatorKeyAsync(user);
        var sharedKey = await userManager.GetAuthenticatorKeyAsync(user);
        if (string.IsNullOrEmpty(sharedKey))
        {
            return Results.Problem("Unable to generate authenticator key.", statusCode: StatusCodes.Status500InternalServerError);
        }

        var formattedKey = FormatKey(sharedKey);
        var otpauthUri = GenerateOtpAuthUri(mfaOptions.Value.Issuer, user.Email ?? user.UserName ?? "user", sharedKey);

        return Results.Ok(new { sharedKey = formattedKey, authenticatorUri = otpauthUri });
    }

    private static async Task<IResult> VerifyAsync(
        HttpContext context,
        MfaVerifyRequest request,
        IValidator<MfaVerifyRequest> validator,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IAuditLogger auditLogger,
        IOptions<MfaOptions> mfaOptions,
        CancellationToken cancellationToken)
    {
        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return Results.ValidationProblem(validationResult.ToDictionary());
        }

        var method = string.IsNullOrWhiteSpace(request.Method)
            ? "authenticator"
            : request.Method.Trim().ToLowerInvariant();

        var options = mfaOptions.Value;
        if (method is "sms" && !options.Sms.Enabled)
        {
            return Results.Problem("SMS MFA challenge is disabled.", statusCode: StatusCodes.Status400BadRequest);
        }

        if (method is "email" && !options.Email.Enabled)
        {
            return Results.Problem("Email MFA challenge is disabled.", statusCode: StatusCodes.Status400BadRequest);
        }

        var authenticateResult = await context.AuthenticateAsync(IdentityConstants.ApplicationScheme);
        if (authenticateResult.Succeeded && authenticateResult.Principal is not null && authenticateResult.Principal.Identity?.IsAuthenticated == true)
        {
            var user = await userManager.GetUserAsync(authenticateResult.Principal);
            if (user is null)
            {
                return Results.Unauthorized();
            }

            context.User = authenticateResult.Principal;

            if (!string.Equals(method, "authenticator", StringComparison.Ordinal))
            {
                return Results.Problem("Only authenticator verification is supported during enrollment.", statusCode: StatusCodes.Status400BadRequest);
            }

            var isValid = await userManager.VerifyTwoFactorTokenAsync(
                user,
                TokenOptions.DefaultAuthenticatorProvider,
                request.Code);

            if (!isValid)
            {
                return Results.Problem("Invalid authenticator code.", statusCode: StatusCodes.Status400BadRequest);
            }

            await userManager.SetTwoFactorEnabledAsync(user, true);
            await userManager.UpdateSecurityStampAsync(user);
            var recoveryCodes = await userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 10);

            await auditLogger.LogAsync(AuditEventTypes.MfaEnabled, user.Id, new { Method = "authenticator" }, cancellationToken);

            return Results.Ok(new
            {
                message = "MFA enabled.",
                recoveryCodes
            });
        }
        var twoFactorUser = await signInManager.GetTwoFactorAuthenticationUserAsync();
        if (twoFactorUser is null)
        {
            return Results.Unauthorized();
        }

        SignInResult signInResult = method switch
        {
            "recovery" => await signInManager.TwoFactorRecoveryCodeSignInAsync(request.Code),
            "sms" => await signInManager.TwoFactorSignInAsync(
                TokenOptions.DefaultPhoneProvider,
                request.Code,
                isPersistent: false,
                rememberClient: request.RememberMachine),
            "email" => await signInManager.TwoFactorSignInAsync(
                TokenOptions.DefaultEmailProvider,
                request.Code,
                isPersistent: false,
                rememberClient: request.RememberMachine),
            _ => await signInManager.TwoFactorAuthenticatorSignInAsync(
                request.Code,
                isPersistent: false,
                rememberClient: request.RememberMachine)
        };

        if (signInResult.Succeeded)
        {
            await auditLogger.LogAsync(AuditEventTypes.MfaVerified, twoFactorUser.Id, new { Method = method }, cancellationToken);
            return Results.Ok(new { message = "MFA verification successful." });
        }

        if (signInResult.IsLockedOut)
        {
            return Results.Problem("User account is locked out.", statusCode: StatusCodes.Status400BadRequest);
        }

        return Results.Problem("Invalid authenticator code.", statusCode: StatusCodes.Status400BadRequest);
    }

    private static async Task<IResult> DisableAsync(
        HttpContext context,
        UserManager<ApplicationUser> userManager,
        IAuditLogger auditLogger,
        CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(context.User);
        if (user is null)
        {
            return Results.Unauthorized();
        }

        var disableResult = await userManager.SetTwoFactorEnabledAsync(user, false);
        if (!disableResult.Succeeded)
        {
            return Results.ValidationProblem(disableResult.ToDictionary());
        }

        await userManager.ResetAuthenticatorKeyAsync(user);
        await userManager.UpdateSecurityStampAsync(user);

        await auditLogger.LogAsync(AuditEventTypes.MfaDisabled, user.Id, null, cancellationToken);

        return Results.Ok(new { message = "MFA disabled." });
    }

    private static async Task<IResult> RegenerateRecoveryCodesAsync(
        HttpContext context,
        UserManager<ApplicationUser> userManager,
        IAuditLogger auditLogger,
        CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(context.User);
        if (user is null)
        {
            return Results.Unauthorized();
        }

        if (!await userManager.GetTwoFactorEnabledAsync(user))
        {
            return Results.Problem("MFA is not enabled for this user.", statusCode: StatusCodes.Status400BadRequest);
        }

        var recoveryCodes = (await userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 10) ?? Array.Empty<string>()).ToArray();
        await auditLogger.LogAsync(AuditEventTypes.MfaRecoveryCodesRegenerated, user.Id, new { Count = recoveryCodes.Length }, cancellationToken);
        return Results.Ok(new { recoveryCodes });
    }

    private static async Task<IResult> ChallengeAsync(
        HttpContext context,
        MfaChallengeRequest request,
        IValidator<MfaChallengeRequest> validator,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IEnumerable<IMfaChallengeSender> challengeSenders,
        ILoggerFactory loggerFactory,
        IOptions<MfaOptions> mfaOptions,
        IAuditLogger auditLogger,
        CancellationToken cancellationToken)
    {
        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return Results.ValidationProblem(validationResult.ToDictionary());
        }

        var method = request.Method.Trim().ToLowerInvariant();

        var options = mfaOptions.Value;
        var enabledSenders = challengeSenders
            .Where(sender => IsMethodEnabled(sender.Method, options))
            .ToList();

        var availableMethods = enabledSenders
            .Select(sender => sender.Method)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (!IsMethodEnabled(method, options))
        {
            var available = availableMethods.Length > 0 ? string.Join(", ", availableMethods) : "none";
            var reason = method switch
            {
                "sms" => "SMS MFA challenge is disabled.",
                "email" => "Email MFA challenge is disabled.",
                _ => $"Unsupported challenge method '{request.Method}'."
            };
            return Results.Problem($"{reason} Available methods: {available}.", statusCode: StatusCodes.Status400BadRequest);
        }

        var challengeSender = enabledSenders.FirstOrDefault(sender =>
            string.Equals(sender.Method, method, StringComparison.OrdinalIgnoreCase));

        if (challengeSender is null)
        {
            var available = availableMethods.Length > 0 ? string.Join(", ", availableMethods) : "none";
            return Results.Problem($"Unsupported challenge method '{request.Method}'. Available methods: {available}.", statusCode: StatusCodes.Status400BadRequest);
        }

        ApplicationUser? user = null;

        var authenticated = await context.AuthenticateAsync(IdentityConstants.ApplicationScheme);
        if (authenticated.Succeeded && authenticated.Principal?.Identity?.IsAuthenticated == true)
        {
            user = await userManager.GetUserAsync(authenticated.Principal);
        }

        if (user is null)
        {
            user = await signInManager.GetTwoFactorAuthenticationUserAsync();
        }

        if (user is null)
        {
            return Results.Unauthorized();
        }

        var logger = loggerFactory.CreateLogger("MfaChallenge");

        var provider = method switch
        {
            "sms" => TokenOptions.DefaultPhoneProvider,
            "email" => TokenOptions.DefaultEmailProvider,
            _ => throw new InvalidOperationException($"Unsupported challenge method '{request.Method}'.")
        };

        try
        {
            var token = await userManager.GenerateTwoFactorTokenAsync(user, provider);
            await challengeSender.SendChallengeAsync(user, token, cancellationToken);
            await auditLogger.LogAsync(AuditEventTypes.MfaChallengeSent, user.Id, new { Method = method }, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex, "Failed to send MFA challenge for {UserId}", user.Id);
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }

        return Results.Accepted(value: new { message = "MFA challenge sent." });
    }

    private static bool IsMethodEnabled(string method, MfaOptions options)
    {
        if (string.Equals(method, "sms", StringComparison.OrdinalIgnoreCase))
        {
            return options.Sms.Enabled;
        }

        if (string.Equals(method, "email", StringComparison.OrdinalIgnoreCase))
        {
            return options.Email.Enabled;
        }

        return true;
    }

    private static string FormatKey(string key)
    {
        var result = new StringBuilder();
        var currentPosition = 0;
        while (currentPosition + 4 < key.Length)
        {
            result.Append(key.AsSpan(currentPosition, 4)).Append(' ');
            currentPosition += 4;
        }

        if (currentPosition < key.Length)
        {
            result.Append(key.AsSpan(currentPosition));
        }

        return result.ToString().ToLowerInvariant();
    }

    private static string GenerateOtpAuthUri(string issuer, string userEmail, string sharedKey)
    {
        return string.Format(
            "otpauth://totp/{0}:{1}?secret={2}&issuer={0}&digits=6",
            Uri.EscapeDataString(issuer),
            Uri.EscapeDataString(userEmail),
            sharedKey);
    }
}

public sealed record MfaVerifyRequest(string Code, string Method = "authenticator", bool RememberMachine = false);

public sealed class MfaVerifyRequestValidator : AbstractValidator<MfaVerifyRequest>
{
    public MfaVerifyRequestValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty()
            .MaximumLength(12);

        RuleFor(x => x.Method)
            .NotEmpty()
            .Must(method =>
            {
                var normalized = method.Trim().ToLowerInvariant();
                return normalized is "authenticator" or "sms" or "email" or "recovery";
            })
            .WithMessage("Method must be one of 'authenticator', 'sms', 'email', or 'recovery'.");
    }
}

public sealed record MfaChallengeRequest(string Method);

public sealed class MfaChallengeRequestValidator : AbstractValidator<MfaChallengeRequest>
{
    public MfaChallengeRequestValidator()
    {
        RuleFor(x => x.Method)
            .NotEmpty()
            .Must(method =>
            {
                var normalized = method.Trim().ToLowerInvariant();
                return normalized is "sms" or "email";
            })
            .WithMessage("Supported challenge methods are 'sms' and 'email'.");
    }
}
