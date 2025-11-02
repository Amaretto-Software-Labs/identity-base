using System;
using Identity.Base.Organisations.Api.Modules;
using Microsoft.AspNetCore.Routing;

namespace Identity.Base.Organisations.Extensions;

public static class EndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapIdentityBaseOrganisationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapOrganisationEndpoints();
        endpoints.MapOrganisationMembershipEndpoints();
        endpoints.MapOrganisationRoleEndpoints();
        endpoints.MapUserOrganisationEndpoints();
        endpoints.MapOrganisationInvitationEndpoints();
        return endpoints;
    }
}
