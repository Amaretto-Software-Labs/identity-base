using System.Linq;
using FluentValidation;
using Identity.Base.Extensions;
using Identity.Base.Identity;
using Identity.Base.Features.Authentication.EmailManagement;
using Identity.Base.Options;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
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
        IAccountEmailService accountEmailService,
        IOptions<RegistrationOptions> registrationOptions,
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

        var logger = loggerFactory.CreateLogger("RegisterUserEndpoint");

        try
        {
            await accountEmailService.SendConfirmationEmailAsync(user, cancellationToken);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
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
}
