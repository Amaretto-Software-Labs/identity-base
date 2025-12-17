using System;
using System.Security.Claims;
using System.Threading;
using Identity.Base.Roles.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using OpenIddict.Abstractions;

namespace Identity.Base.Roles.Endpoints;

public static class UserPermissionEndpoints
{
    public static IEndpointRouteBuilder MapIdentityRolesUserEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/users/me/permissions", async (ClaimsPrincipal principal, IPermissionResolver resolver, CancellationToken cancellationToken) =>
        {
            var userIdClaim = principal.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? principal.FindFirstValue(OpenIddictConstants.Claims.Subject);
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
