using System.Collections.Generic;
using System.Text;
using FluentValidation;
using Identity.Base.Extensions;
using Identity.Base.Features.Email;
using Identity.Base.Identity;
using Identity.Base.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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
        CancellationToken cancellationToken)
    {
        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return Results.ValidationProblem(validationResult.ToDictionary());
        }

        var email = DecodeEmail(request.Email);
        if (!TryDecodeToken(request.Token, out var token))
        {
            return Results.Problem("Invalid email confirmation token.", statusCode: StatusCodes.Status400BadRequest);
        }

        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            return Results.Problem("Invalid email confirmation token.", statusCode: StatusCodes.Status400BadRequest);
        }

        if (user.EmailConfirmed)
        {
            return Results.Ok(new { message = "Email already confirmed." });
        }

        var result = await userManager.ConfirmEmailAsync(user, token);
        if (!result.Succeeded)
        {
            return Results.ValidationProblem(result.ToDictionary());
        }

        return Results.Ok(new { message = "Email confirmed." });
    }

    private static async Task<IResult> ResendConfirmationEmailAsync(
        ResendConfirmationRequest request,
        IValidator<ResendConfirmationRequest> validator,
        UserManager<ApplicationUser> userManager,
        ITemplatedEmailSender emailSender,
        IOptions<RegistrationOptions> registrationOptions,
        IOptions<MailJetOptions> mailJetOptions,
        ILoggerFactory loggerFactory,
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

        var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
        var encodedToken = EncodeToken(token);
        var confirmationUrl = BuildUrl(
            registrationOptions.Value.ConfirmationUrlTemplate,
            encodedToken,
            user.Email!);

        var mailOptions = mailJetOptions.Value;
        if (mailOptions.Templates.Confirmation <= 0)
        {
            return Results.Problem("Confirmation template is not configured.", statusCode: StatusCodes.Status500InternalServerError);
        }

        var variables = new Dictionary<string, object?>
        {
            ["email"] = user.Email,
            ["displayName"] = user.DisplayName ?? user.Email,
            ["confirmationUrl"] = confirmationUrl
        };

        var email = new TemplatedEmail(
            user.Email!,
            user.DisplayName ?? user.Email!,
            mailOptions.Templates.Confirmation,
            variables,
            "Confirm your Identity Base account");

        var logger = loggerFactory.CreateLogger("EmailManagementEndpoints");

        try
        {
            await emailSender.SendAsync(email, cancellationToken);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogError(exception, "Failed to resend confirmation email for {Email}", user.Email);
            return Results.Problem("Failed to resend confirmation email.", statusCode: StatusCodes.Status500InternalServerError);
        }

        return Results.Accepted();
    }

    private static async Task<IResult> ForgotPasswordAsync(
        ForgotPasswordRequest request,
        IValidator<ForgotPasswordRequest> validator,
        UserManager<ApplicationUser> userManager,
        ITemplatedEmailSender emailSender,
        IOptions<RegistrationOptions> registrationOptions,
        IOptions<MailJetOptions> mailJetOptions,
        ILoggerFactory loggerFactory,
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

        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        var encodedToken = EncodeToken(token);
        var resetUrl = BuildUrl(
            registrationOptions.Value.PasswordResetUrlTemplate,
            encodedToken,
            user.Email!);

        var mailOptions = mailJetOptions.Value;
        if (mailOptions.Templates.PasswordReset <= 0)
        {
            return Results.Problem("Password reset template is not configured.", statusCode: StatusCodes.Status500InternalServerError);
        }

        var variables = new Dictionary<string, object?>
        {
            ["email"] = user.Email,
            ["displayName"] = user.DisplayName ?? user.Email,
            ["resetUrl"] = resetUrl
        };

        var email = new TemplatedEmail(
            user.Email!,
            user.DisplayName ?? user.Email!,
            mailOptions.Templates.PasswordReset,
            variables,
            "Reset your Identity Base password");

        var logger = loggerFactory.CreateLogger("EmailManagementEndpoints");

        try
        {
            await emailSender.SendAsync(email, cancellationToken);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogError(exception, "Failed to send password reset email for {Email}", user.Email);
            return Results.Problem("Failed to dispatch password reset email.", statusCode: StatusCodes.Status500InternalServerError);
        }

        return Results.Accepted();
    }

    private static async Task<IResult> ResetPasswordAsync(
        ResetPasswordRequest request,
        IValidator<ResetPasswordRequest> validator,
        UserManager<ApplicationUser> userManager,
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

        var email = DecodeEmail(request.Email);
        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            return Results.Problem("Invalid password reset token.", statusCode: StatusCodes.Status400BadRequest);
        }

        var result = await userManager.ResetPasswordAsync(user, token, request.Password);
        if (!result.Succeeded)
        {
            return Results.ValidationProblem(result.ToDictionary());
        }

        return Results.Ok(new { message = "Password reset successful." });
    }

    private static string EncodeToken(string token)
        => WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));

    private static string BuildUrl(string template, string token, string email)
        => template
            .Replace("{token}", token, StringComparison.Ordinal)
            .Replace("{email}", WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(email)), StringComparison.Ordinal);

    private static string DecodeEmail(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        try
        {
            var bytes = WebEncoders.Base64UrlDecode(value);
            return Encoding.UTF8.GetString(bytes);
        }
        catch (FormatException)
        {
            return value;
        }
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

public sealed record ConfirmEmailRequest(string Email, string Token);

public sealed record ResendConfirmationRequest(string Email);

public sealed record ForgotPasswordRequest(string Email);

public sealed record ResetPasswordRequest(string Email, string Token, string Password);

public sealed class ConfirmEmailRequestValidator : AbstractValidator<ConfirmEmailRequest>
{
    public ConfirmEmailRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty();

        RuleFor(x => x.Token)
            .NotEmpty();
    }
}

public sealed class ResendConfirmationRequestValidator : AbstractValidator<ResendConfirmationRequest>
{
    public ResendConfirmationRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress();
    }
}

public sealed class ForgotPasswordRequestValidator : AbstractValidator<ForgotPasswordRequest>
{
    public ForgotPasswordRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress();
    }
}

public sealed class ResetPasswordRequestValidator : AbstractValidator<ResetPasswordRequest>
{
    public ResetPasswordRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty();

        RuleFor(x => x.Token)
            .NotEmpty();

        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(12);
    }
}
