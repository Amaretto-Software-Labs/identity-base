using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Identity.Base.Roles.Claims;
using Identity.Base.Organizations.Services;
using Identity.Base.Organizations.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using System.Globalization;

namespace Identity.Base.Organizations.Authorization;

public sealed class OrganizationPermissionRequirement : IAuthorizationRequirement
{
    public OrganizationPermissionRequirement(string permission)
    {
        if (string.IsNullOrWhiteSpace(permission))
        {
            throw new ArgumentException("Permission is required.", nameof(permission));
        }

        Permission = permission;
    }

    public string Permission { get; }
}

public sealed class OrganizationPermissionAuthorizationHandler : AuthorizationHandler<OrganizationPermissionRequirement>
{
    private readonly IOrganizationPermissionResolver _permissionResolver;

    public OrganizationPermissionAuthorizationHandler(IOrganizationPermissionResolver permissionResolver)
    {
        _permissionResolver = permissionResolver ?? throw new ArgumentNullException(nameof(permissionResolver));
    }

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, OrganizationPermissionRequirement requirement)
    {
        if (context.User is null)
        {
            return;
        }

        var hasPermission = context.User
            .FindAll(RoleClaimTypes.Permissions)
            .SelectMany(claim => claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Contains(requirement.Permission, StringComparer.OrdinalIgnoreCase);

        if (hasPermission)
        {
            context.Succeed(requirement);
            return;
        }

        var organizationId = ResolveOrganizationId(context);
        var userIdValue = context.User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!organizationId.HasValue || organizationId.Value == Guid.Empty)
        {
            return;
        }

        if (!Guid.TryParse(userIdValue, out var userId) || userId == Guid.Empty)
        {
            return;
        }

        var permissions = await _permissionResolver
            .GetPermissionsAsync(organizationId.Value, userId, CancellationToken.None)
            .ConfigureAwait(false);

        if (permissions.Count == 0)
        {
            return;
        }

        if (permissions.Contains(requirement.Permission, StringComparer.OrdinalIgnoreCase))
        {
            context.Succeed(requirement);
        }
    }

    private static Guid? ResolveOrganizationId(AuthorizationHandlerContext context)
    {
        var httpContext = context.Resource as HttpContext;
        if (httpContext is not null)
        {
            if (httpContext.Request.RouteValues.TryGetValue("organizationId", out var value) &&
                value is not null)
            {
                var text = Convert.ToString(value, CultureInfo.InvariantCulture);
                if (Guid.TryParse(text, out var routeOrganizationId))
                {
                    return routeOrganizationId;
                }
            }
        }

        var claimValue = context.User.FindFirstValue(OrganizationClaimTypes.OrganizationId);
        if (Guid.TryParse(claimValue, out var claimOrganizationId))
        {
            return claimOrganizationId;
        }

        return null;
    }
}
