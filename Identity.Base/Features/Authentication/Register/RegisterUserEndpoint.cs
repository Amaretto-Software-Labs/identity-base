using System.Linq;
using System.Text;
using FluentValidation;
using Identity.Base.Extensions;
using Identity.Base.Identity;
using Identity.Base.Options;
using Identity.Base.Features.Email;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Identity.Base.Features.Authentication.Register;

public static class RegisterUserEndpoint
{
    public static RouteGroupBuilder MapRegisterUserEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/register", HandleAsync)
            .WithName("RegisterUser")
            .WithSummary("Registers a new user with metadata and triggers confirmation email.")
            .Produces(StatusCodes.Status202Accepted)
            .ProducesValidationProblem()
            .WithTags("Authentication");

        return group;
    }

    private static async Task<IResult> HandleAsync(
        RegisterUserRequest request,
        IValidator<RegisterUserRequest> validator,
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

        var options = registrationOptions.Value;

        var user = new ApplicationUser
        {
            Email = request.Email,
            UserName = request.Email,
            DisplayName = ResolveDisplayName(request, options)
        };

        user.SetProfileMetadata(request.Metadata);

        var createResult = await userManager.CreateAsync(user, request.Password);
        if (!createResult.Succeeded)
        {
            return Results.ValidationProblem(createResult.ToDictionary());
        }

        var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
        var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
        var confirmationUrl = BuildConfirmationUrl(options.ConfirmationUrlTemplate, encodedToken, user.Email!);

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

        var templatedEmail = new TemplatedEmail(
            user.Email!,
            user.DisplayName ?? user.Email!,
            mailOptions.Templates.Confirmation,
            variables,
            "Confirm your Identity Base account");

        try
        {
            await emailSender.SendAsync(templatedEmail, cancellationToken);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            var logger = loggerFactory.CreateLogger("RegisterUserEndpoint");
            logger.LogError(exception, "Failed to send confirmation email for {Email}", user.Email);
            return Results.Problem("Failed to dispatch confirmation email.", statusCode: StatusCodes.Status500InternalServerError);
        }

        var correlationId = Guid.NewGuid().ToString("N");
        return Results.Accepted($"/auth/register/{correlationId}", new { correlationId });
    }

    private static string? ResolveDisplayName(RegisterUserRequest request, RegistrationOptions options)
    {
        var preferredField = options.ProfileFields.FirstOrDefault(field => field.Name.Equals("displayName", StringComparison.OrdinalIgnoreCase));
        if (preferredField is not null && request.Metadata.TryGetValue(preferredField.Name, out var displayName) && !string.IsNullOrWhiteSpace(displayName))
        {
            return displayName;
        }

        return null;
    }

    private static string BuildConfirmationUrl(string template, string token, string email)
        => template
            .Replace("{token}", token, StringComparison.Ordinal)
            .Replace("{email}", WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(email)), StringComparison.Ordinal);
}
