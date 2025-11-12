using System;
using System.Text;
using FluentValidation;
using Identity.Base.Extensions;
using Identity.Base.Identity;
using Identity.Base.Logging;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Identity.Base.Lifecycle;

namespace Identity.Base.Features.Authentication.EmailManagement;

public static class EmailManagementEndpoints
{
    public static RouteGroupBuilder MapEmailManagementEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/confirm-email", ConfirmEmailAsync)
            .WithName("ConfirmEmail")
            .WithSummary("Confirms a user's email address using the confirmation token.")
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithTags("Authentication");

        group.MapPost("/resend-confirmation", ResendConfirmationEmailAsync)
            .WithName("ResendConfirmationEmail")
            .WithSummary("Resends the confirmation email when the account is not yet confirmed.")
            .Produces(StatusCodes.Status202Accepted)
            .WithTags("Authentication");

        group.MapPost("/forgot-password", ForgotPasswordAsync)
            .WithName("ForgotPassword")
            .WithSummary("Generates a password reset token and sends the reset email if the account exists.")
            .Produces(StatusCodes.Status202Accepted)
            .WithTags("Authentication");

        group.MapPost("/reset-password", ResetPasswordAsync)
            .WithName("ResetPassword")
            .WithSummary("Resets the user's password using the provided token.")
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithTags("Authentication");

        return group;
    }

    private static async Task<IResult> ConfirmEmailAsync(
        ConfirmEmailRequest request,
        IValidator<ConfirmEmailRequest> validator,
        UserManager<ApplicationUser> userManager,
        IUserLifecycleHookDispatcher lifecycleDispatcher,
        CancellationToken cancellationToken)
    {
        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return Results.ValidationProblem(validationResult.ToDictionary());
        }

        if (!TryDecodeToken(request.Token, out var token))
        {
            return Results.Problem("Invalid email confirmation token.", statusCode: StatusCodes.Status400BadRequest);
        }

        var user = await userManager.FindByIdAsync(request.UserId);
        if (user is null)
        {
            return Results.Problem("Invalid email confirmation token.", statusCode: StatusCodes.Status400BadRequest);
        }

        if (user.EmailConfirmed)
        {
            return Results.Ok(new { message = "Email already confirmed." });
        }

        var lifecycleContext = new UserLifecycleContext(
            UserLifecycleEvent.EmailConfirmation,
            user,
            Source: nameof(ConfirmEmailAsync));

        try
        {
            await lifecycleDispatcher.EnsureCanConfirmEmailAsync(lifecycleContext, cancellationToken);
        }
        catch (LifecycleHookRejectedException exception)
        {
            return Results.Problem(exception.Message, statusCode: StatusCodes.Status400BadRequest);
        }

        var result = await userManager.ConfirmEmailAsync(user, token);
        if (!result.Succeeded)
        {
            return Results.ValidationProblem(result.ToDictionary());
        }

        await lifecycleDispatcher.NotifyUserEmailConfirmedAsync(lifecycleContext, cancellationToken);

        return Results.Ok(new { message = "Email confirmed." });
    }

    private static async Task<IResult> ResendConfirmationEmailAsync(
        ResendConfirmationRequest request,
        IValidator<ResendConfirmationRequest> validator,
        UserManager<ApplicationUser> userManager,
        IAccountEmailService accountEmailService,
        ILoggerFactory loggerFactory,
        ILogSanitizer logSanitizer,
        CancellationToken cancellationToken)
    {
        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return Results.ValidationProblem(validationResult.ToDictionary());
        }

        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null)
        {
            return Results.Accepted();
        }

        if (await userManager.IsEmailConfirmedAsync(user))
        {
            return Results.Ok(new { message = "Email already confirmed." });
        }

        var logger = loggerFactory.CreateLogger(typeof(EmailManagementEndpoints).FullName!);

        try
        {
            await accountEmailService.SendConfirmationEmailAsync(user, cancellationToken);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogError(exception, "Failed to resend confirmation email for {Email}", logSanitizer.RedactEmail(user.Email));
            return Results.Problem("Failed to resend confirmation email.", statusCode: StatusCodes.Status500InternalServerError);
        }

        return Results.Accepted();
    }

    private static async Task<IResult> ForgotPasswordAsync(
        ForgotPasswordRequest request,
        IValidator<ForgotPasswordRequest> validator,
        UserManager<ApplicationUser> userManager,
        IAccountEmailService accountEmailService,
        ILoggerFactory loggerFactory,
        ILogSanitizer logSanitizer,
        CancellationToken cancellationToken)
    {
        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return Results.ValidationProblem(validationResult.ToDictionary());
        }

        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null || !await userManager.IsEmailConfirmedAsync(user))
        {
            return Results.Accepted();
        }

        var logger = loggerFactory.CreateLogger(typeof(EmailManagementEndpoints).FullName!);

        try
        {
            await accountEmailService.SendPasswordResetEmailAsync(user, cancellationToken);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogError(exception, "Failed to send password reset email for {Email}", logSanitizer.RedactEmail(user.Email));
            return Results.Problem("Failed to dispatch password reset email.", statusCode: StatusCodes.Status500InternalServerError);
        }

        return Results.Accepted();
    }

    private static async Task<IResult> ResetPasswordAsync(
        ResetPasswordRequest request,
        IValidator<ResetPasswordRequest> validator,
        UserManager<ApplicationUser> userManager,
        IUserLifecycleHookDispatcher lifecycleDispatcher,
        CancellationToken cancellationToken)
    {
        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return Results.ValidationProblem(validationResult.ToDictionary());
        }

        if (!TryDecodeToken(request.Token, out var token))
        {
            return Results.Problem("Invalid password reset token.", statusCode: StatusCodes.Status400BadRequest);
        }

        var user = await userManager.FindByIdAsync(request.UserId);
        if (user is null)
        {
            return Results.Problem("Invalid password reset token.", statusCode: StatusCodes.Status400BadRequest);
        }

        var lifecycleContext = new UserLifecycleContext(
            UserLifecycleEvent.PasswordReset,
            user,
            ActorUserId: user.Id,
            Source: nameof(ResetPasswordAsync),
            Items: new Dictionary<string, object?>
            {
                ["ResetFlow"] = "Token"
            });

        try
        {
            await lifecycleDispatcher.EnsureCanResetPasswordAsync(lifecycleContext, cancellationToken);
        }
        catch (LifecycleHookRejectedException exception)
        {
            return Results.Problem(exception.Message, statusCode: StatusCodes.Status400BadRequest);
        }

        var result = await userManager.ResetPasswordAsync(user, token, request.Password);
        if (!result.Succeeded)
        {
            return Results.ValidationProblem(result.ToDictionary());
        }

        await lifecycleDispatcher.NotifyUserPasswordResetAsync(lifecycleContext, cancellationToken);

        return Results.Ok(new { message = "Password reset successful." });
    }

    private static bool TryDecodeToken(string token, out string decoded)
    {
        try
        {
            decoded = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(token));
            return true;
        }
        catch (FormatException)
        {
            decoded = string.Empty;
            return false;
        }
    }
}

internal sealed record ConfirmEmailRequest(string UserId, string Token);

internal sealed record ResendConfirmationRequest(string Email);

internal sealed record ForgotPasswordRequest(string Email);

internal sealed record ResetPasswordRequest(string UserId, string Token, string Password);

internal sealed class ConfirmEmailRequestValidator : AbstractValidator<ConfirmEmailRequest>
{
    public ConfirmEmailRequestValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty();

        RuleFor(x => x.Token)
            .NotEmpty();
    }
}

internal sealed class ResendConfirmationRequestValidator : AbstractValidator<ResendConfirmationRequest>
{
    public ResendConfirmationRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress();
    }
}

internal sealed class ForgotPasswordRequestValidator : AbstractValidator<ForgotPasswordRequest>
{
    public ForgotPasswordRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress();
    }
}

internal sealed class ResetPasswordRequestValidator : AbstractValidator<ResetPasswordRequest>
{
    public ResetPasswordRequestValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty();
        RuleFor(x => x.UserId)
            .Must(value => Guid.TryParse(value, out _))
            .WithMessage("UserId must be a valid GUID.");

        RuleFor(x => x.Token)
            .NotEmpty();

        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(12);
    }
}
