using Identity.Base.Features.Authentication.Register;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Identity.Base.Features.Authentication;

public static class AuthenticationEndpoints
{
    public static RouteGroupBuilder MapAuthenticationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/auth");

        group.MapRegisterUserEndpoint();

        return group;
    }
}
