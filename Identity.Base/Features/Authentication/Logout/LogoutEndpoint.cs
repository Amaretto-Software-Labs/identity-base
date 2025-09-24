using Identity.Base.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;

namespace Identity.Base.Features.Authentication.Logout;

public static class LogoutEndpoint
{
    public static RouteGroupBuilder MapLogoutEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/logout", HandleAsync)
            .WithName("Logout")
            .WithSummary("Signs the current user out of the Identity cookie session.")
            .Produces(StatusCodes.Status200OK)
            .WithTags("Authentication");

        return group;
    }

    private static async Task<IResult> HandleAsync(SignInManager<ApplicationUser> signInManager)
    {
        await signInManager.SignOutAsync();
        return Results.Ok(new { message = "Logout successful." });
    }
}
