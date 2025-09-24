using System;
using System.Collections.Generic;
using System.Linq;
using Identity.Base.Identity;
using Identity.Base.Options;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;

namespace Identity.Base.Features.Authentication.Login;

public static class LoginEndpoint
{

    public static RouteGroupBuilder MapLoginEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/login", HandleAsync)
            .WithName("Login")
            .WithSummary("Authenticates a user and establishes an Identity cookie for subsequent authorization flows.")
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithTags("Authentication");

        return group;
    }

    private static async Task<IResult> HandleAsync(
        HttpContext httpContext,
        LoginRequest request,
        IValidator<LoginRequest> validator,
        IOpenIddictApplicationManager applicationManager,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IOptions<MfaOptions> mfaOptions,
        CancellationToken cancellationToken)
    {
        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return Results.ValidationProblem(validationResult.ToDictionary());
        }

        var application = await applicationManager.FindByClientIdAsync(request.ClientId, cancellationToken);
        if (application is null)
        {
            return Results.Problem("Unknown client_id.", statusCode: StatusCodes.Status400BadRequest);
        }

        var clientType = await applicationManager.GetClientTypeAsync(application, cancellationToken);
        if (clientType == OpenIddictConstants.ClientTypes.Confidential)
        {
            if (string.IsNullOrWhiteSpace(request.ClientSecret) ||
                !await applicationManager.ValidateClientSecretAsync(application, request.ClientSecret!, cancellationToken))
            {
                return Results.Problem("Invalid client credentials.", statusCode: StatusCodes.Status400BadRequest);
            }
        }

        var user = await userManager.FindByEmailAsync(request.Email) ?? await userManager.FindByNameAsync(request.Email);
        if (user is null)
        {
            return Results.Problem("Invalid credentials.", statusCode: StatusCodes.Status400BadRequest);
        }

        var signInResult = await signInManager.PasswordSignInAsync(
            user.UserName ?? request.Email,
            request.Password,
            isPersistent: false,
            lockoutOnFailure: true);

        if (signInResult.IsLockedOut)
        {
            return Results.Problem("User account is locked out.", statusCode: StatusCodes.Status400BadRequest);
        }

        if (signInResult.IsNotAllowed)
        {
            return Results.Problem("Email must be confirmed before logging in.", statusCode: StatusCodes.Status400BadRequest);
        }

        if (signInResult.RequiresTwoFactor)
        {
            var providers = await userManager.GetValidTwoFactorProvidersAsync(user);
            var methods = providers
                .Select(MapTwoFactorProvider)
                .Where(static method => method is not null)
                .Cast<string>()
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            methods.Add("recovery");

            var options = mfaOptions.Value;
            methods = methods
                .Where(method => method switch
                {
                    "sms" => options.Sms.Enabled,
                    "email" => options.Email.Enabled,
                    _ => true
                })
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            return Results.Ok(new
            {
                requiresTwoFactor = true,
                methods
            });
        }

        if (!signInResult.Succeeded)
        {
            return Results.Problem("Invalid credentials.", statusCode: StatusCodes.Status400BadRequest);
        }

        return Results.Ok(new
        {
            message = "Login successful. Continue with authorization code flow.",
            clientId = request.ClientId
        });
    }

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
}
