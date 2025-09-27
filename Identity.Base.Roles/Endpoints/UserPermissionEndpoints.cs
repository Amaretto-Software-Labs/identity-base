using System;
using System.Security.Claims;
using System.Threading;
using Identity.Base.Roles.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Identity.Base.Roles.Endpoints;

public static class UserPermissionEndpoints
{
    public static IEndpointRouteBuilder MapIdentityRolesUserEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/users/me/permissions", async (ClaimsPrincipal principal, IPermissionResolver resolver, CancellationToken cancellationToken) =>
        {
            var userIdClaim = principal.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            {
                return Results.Unauthorized();
            }

            var permissions = await resolver.GetEffectivePermissionsAsync(userId, cancellationToken).ConfigureAwait(false);
            return Results.Ok(new { permissions });
        })
        .RequireAuthorization();

        return endpoints;
    }
}
