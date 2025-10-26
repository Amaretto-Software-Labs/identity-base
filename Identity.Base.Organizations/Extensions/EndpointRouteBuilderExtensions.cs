using System;
using Identity.Base.Organizations.Api.Modules;
using Microsoft.AspNetCore.Routing;

namespace Identity.Base.Organizations.Extensions;

public static class EndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapIdentityBaseOrganizationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapOrganizationEndpoints();
        endpoints.MapOrganizationMembershipEndpoints();
        endpoints.MapOrganizationRoleEndpoints();
        endpoints.MapUserOrganizationEndpoints();
        return endpoints;
    }
}
