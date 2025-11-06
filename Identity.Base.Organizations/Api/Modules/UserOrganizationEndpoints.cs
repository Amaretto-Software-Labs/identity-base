using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Identity.Base.Abstractions.Pagination;
using Identity.Base.Organizations.Abstractions;
using Identity.Base.Organizations.Api.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Identity.Base.Organizations.Api.Modules;

public static class UserOrganizationEndpoints
{
    public static IEndpointRouteBuilder MapUserOrganizationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapGet("/users/me/organizations", async (
            ClaimsPrincipal principal,
            Guid? tenantId,
            [AsParameters] UserOrganizationListQuery query,
            IOrganizationMembershipService membershipService,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(principal, out var userId))
            {
                return Results.Unauthorized();
            }

            var pageRequest = query.ToPageRequest();
            var includeArchived = query.IncludeArchived ?? false;

            var result = await membershipService
                .GetMembershipsForUserAsync(userId, tenantId, pageRequest, includeArchived, cancellationToken)
                .ConfigureAwait(false);

            var items = result.Items
                .Select(OrganizationApiMapper.ToUserOrganizationMembershipDto)
                .ToList();

            var response = new PagedResult<UserOrganizationMembershipDto>(
                result.Page,
                result.PageSize,
                result.TotalCount,
                items);

            return Results.Ok(response);
        })
        .RequireAuthorization();

        return endpoints;
    }

    private static bool TryGetUserId(ClaimsPrincipal principal, out Guid userId)
    {
        var value = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out userId);
    }
}
