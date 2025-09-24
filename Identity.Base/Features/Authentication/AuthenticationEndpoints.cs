using Identity.Base.Features.Authentication.Login;
using Identity.Base.Features.Authentication.Logout;
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
        group.MapLoginEndpoint();
        group.MapLogoutEndpoint();

        return group;
    }
}
