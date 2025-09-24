using System;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using Identity.Base.Identity;

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
}
