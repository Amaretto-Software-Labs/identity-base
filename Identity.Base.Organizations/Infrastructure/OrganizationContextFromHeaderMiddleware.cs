using System.Security.Claims;
using Identity.Base.Organizations.Abstractions;
using Identity.Base.Organizations.Authorization;
using Identity.Base.Organizations.Claims;
using Identity.Base.Organizations.Data;
using Identity.Base.Roles.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Identity.Base.Organizations.Infrastructure;

public sealed class OrganizationContextFromHeaderMiddleware(RequestDelegate next, string headerName)
{
    private readonly RequestDelegate _next = next ?? throw new ArgumentNullException(nameof(next));
    private readonly string _headerName = string.IsNullOrWhiteSpace(headerName) ? OrganizationContextHeaderNames.OrganizationId : headerName;

    public async Task InvokeAsync(
        HttpContext httpContext,
        IOrganizationContextAccessor contextAccessor,
        OrganizationDbContext organizationDbContext)
    {
        if (httpContext is null)
        {
            throw new ArgumentNullException(nameof(httpContext));
        }

        if (httpContext.Request.Path.StartsWithSegments("/admin/organizations", StringComparison.OrdinalIgnoreCase))
        {
            await _next(httpContext).ConfigureAwait(false);
            return;
        }

        if (httpContext.User?.Identity?.IsAuthenticated == true &&
            httpContext.Request.Headers.TryGetValue(_headerName, out var headerValues))
        {
            var headerValue = headerValues.FirstOrDefault();
            if (string.IsNullOrWhiteSpace(headerValue) || !Guid.TryParse(headerValue, out var requestedOrganizationId))
            {
                httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }

            var userIdClaim = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdClaim, out var userId) || userId == Guid.Empty)
            {
                httpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }

            var userHasAdminAccess = HasAdminPermissions(httpContext.User);

            if (!userHasAdminAccess && !UserHasMembershipClaim(httpContext.User, requestedOrganizationId))
            {
                httpContext.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }

            if (!userHasAdminAccess)
            {
                var membershipExists = await organizationDbContext.OrganizationMemberships
                    .AsNoTracking()
                    .AnyAsync(membership => membership.OrganizationId == requestedOrganizationId && membership.UserId == userId, httpContext.RequestAborted)
                    .ConfigureAwait(false);

                if (!membershipExists)
                {
                    httpContext.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return;
                }
            }

            var organization = await organizationDbContext.Organizations
                .AsNoTracking()
                .FirstOrDefaultAsync(entity => entity.Id == requestedOrganizationId, httpContext.RequestAborted)
                .ConfigureAwait(false);

            if (organization is null)
            {
                httpContext.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            var context = new OrganizationContext(
                organization.Id,
                organization.TenantId,
                organization.Slug,
                organization.DisplayName,
                organization.Metadata);

            using (contextAccessor.BeginScope(context))
            {
                await _next(httpContext).ConfigureAwait(false);
            }
            return;
        }

        await _next(httpContext).ConfigureAwait(false);
    }

    private static bool HasAdminPermissions(ClaimsPrincipal principal)
    {
        return principal.HasAnyPermission(new[]
        {
            AdminOrganizationPermissions.OrganizationsManage,
            AdminOrganizationPermissions.OrganizationsRead
        });
    }

    private static bool UserHasMembershipClaim(ClaimsPrincipal principal, Guid organizationId)
    {
        return principal.HasOrganizationMembership(organizationId);
    }
}
