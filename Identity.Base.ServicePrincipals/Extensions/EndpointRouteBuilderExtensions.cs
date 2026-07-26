using Identity.Base.ServicePrincipals.Api;
using Microsoft.AspNetCore.Routing;

namespace Identity.Base.ServicePrincipals.Extensions;

public static class EndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapIdentityBaseServicePrincipalEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        endpoints.MapServicePrincipalEndpoints();
        return endpoints;
    }
}
