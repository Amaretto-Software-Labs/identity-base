using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Identity.Base.Features.Email;

public static class EmailEndpoints
{
    public static RouteGroupBuilder MapEmailEndpoints(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGroup("/email");
    }
}
