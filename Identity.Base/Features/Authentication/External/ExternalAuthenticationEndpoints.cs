using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using OpenIddict.Validation.AspNetCore;

namespace Identity.Base.Features.Authentication.External;

public static class ExternalAuthenticationEndpoints
{
    public static RouteGroupBuilder MapExternalAuthenticationEndpoints(this RouteGroupBuilder group)
    {
        var external = group.MapGroup("/external");

        external.MapGet("/{provider}/start", StartAsync)
            .WithName("StartExternalAuthentication")
            .WithSummary("Starts an external authentication challenge for the specified provider.")
            .Produces(StatusCodes.Status302Found)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithTags("Authentication");

        external.MapGet("/{provider}/callback", CallbackAsync)
            .WithName("CompleteExternalAuthentication")
            .WithSummary("Handles the callback from an external authentication provider.")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status302Found)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithTags("Authentication");

        external.MapDelete("/{provider}", UnlinkAsync)
            .RequireAuthorization(new AuthorizeAttribute
            {
                AuthenticationSchemes = $"{IdentityConstants.ApplicationScheme},{OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme}"
            })
            .WithName("UnlinkExternalAuthentication")
            .WithSummary("Removes a linked external authentication provider from the current user.")
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .WithTags("Authentication");

        return group;
    }

    private static Task<IResult> StartAsync(
        HttpContext context,
        ExternalAuthenticationService service,
        string provider,
        [AsParameters] ExternalStartRequest request,
        CancellationToken cancellationToken)
    {
        return service.StartAsync(context, provider, request.ReturnUrl, request.Mode, cancellationToken);
    }

    private static Task<IResult> CallbackAsync(
        HttpContext context,
        ExternalAuthenticationService service,
        string provider,
        CancellationToken cancellationToken)
    {
        return service.HandleCallbackAsync(context, provider, cancellationToken);
    }

    private static Task<IResult> UnlinkAsync(
        HttpContext context,
        ExternalAuthenticationService service,
        string provider,
        CancellationToken cancellationToken)
    {
        return service.UnlinkAsync(context, provider, cancellationToken);
    }

    private sealed record ExternalStartRequest(string? ReturnUrl, string Mode = ExternalAuthenticationConstants.ModeLogin);
}
