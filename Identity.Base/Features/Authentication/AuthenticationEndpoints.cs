using Identity.Base.Features.Authentication.External;
using Identity.Base.Features.Authentication.Login;
using Identity.Base.Features.Authentication.EmailManagement;
using Identity.Base.Features.Authentication.Logout;
using Identity.Base.Features.Authentication.Mfa;
using Identity.Base.Features.Authentication.Register;
using Identity.Base.Features.Security;
using Identity.Base.Options;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using System.Linq;

namespace Identity.Base.Features.Authentication;

public static class AuthenticationEndpoints
{
    public static RouteGroupBuilder MapAuthenticationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints
            .MapGroup("/auth")
            .AddEndpointFilter<BrowserOriginGuardEndpointFilter>();

        group.MapRegisterUserEndpoint();
        group.MapLoginEndpoint();
        group.MapLogoutEndpoint();
        group.MapMfaEndpoints();
        group.MapGet("/profile-schema", GetProfileSchema)
            .WithName("GetProfileSchema")
            .WithSummary("Returns the configured profile field definitions.")
            .Produces(StatusCodes.Status200OK)
            .WithTags("Authentication");
        group.MapExternalAuthenticationEndpoints();
        group.MapEmailManagementEndpoints();

        return group;
    }

    private static IResult GetProfileSchema(IOptions<RegistrationOptions> registrationOptions)
    {
        var fields = registrationOptions.Value.ProfileFields
            .Select(field => new ProfileSchemaField(
                field.Name,
                field.DisplayName,
                field.Required,
                field.MaxLength,
                field.Pattern,
                "string"))
            .ToArray();

        return Results.Ok(new ProfileSchemaResponse(fields));
    }
}

internal sealed record ProfileSchemaResponse(IReadOnlyCollection<ProfileSchemaField> Fields);

internal sealed record ProfileSchemaField(
    string Name,
    string DisplayName,
    bool Required,
    int MaxLength,
    string? Pattern,
    string Type);
