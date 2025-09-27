using Identity.Base.Admin.Features.AdminUsers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Identity.Base.Admin.Endpoints;

public static class AdminEndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapIdentityAdminEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapAdminUserEndpoints();
        return endpoints;
    }
}
