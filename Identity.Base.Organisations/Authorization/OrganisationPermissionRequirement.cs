using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Identity.Base.Roles.Claims;
using Identity.Base.Organisations.Services;
using Identity.Base.Organisations.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using System.Globalization;

namespace Identity.Base.Organisations.Authorization;

public sealed class OrganisationPermissionRequirement : IAuthorizationRequirement
{
    public OrganisationPermissionRequirement(string permission)
    {
        if (string.IsNullOrWhiteSpace(permission))
        {
            throw new ArgumentException("Permission is required.", nameof(permission));
        }

        Permission = permission;
    }

    public string Permission { get; }
}

public sealed class OrganisationPermissionAuthorizationHandler : AuthorizationHandler<OrganisationPermissionRequirement>
{
    private readonly IOrganisationPermissionResolver _permissionResolver;

    public OrganisationPermissionAuthorizationHandler(IOrganisationPermissionResolver permissionResolver)
    {
        _permissionResolver = permissionResolver ?? throw new ArgumentNullException(nameof(permissionResolver));
    }

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, OrganisationPermissionRequirement requirement)
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

        var organisationId = ResolveOrganisationId(context);
        var userIdValue = context.User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!organisationId.HasValue || organisationId.Value == Guid.Empty)
        {
            return;
        }

        if (!Guid.TryParse(userIdValue, out var userId) || userId == Guid.Empty)
        {
            return;
        }

        var permissions = await _permissionResolver
            .GetPermissionsAsync(organisationId.Value, userId, CancellationToken.None)
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

    private static Guid? ResolveOrganisationId(AuthorizationHandlerContext context)
    {
        var httpContext = context.Resource as HttpContext;
        if (httpContext is not null)
        {
            if (httpContext.Request.RouteValues.TryGetValue("organisationId", out var value) &&
                value is not null)
            {
                var text = Convert.ToString(value, CultureInfo.InvariantCulture);
                if (Guid.TryParse(text, out var routeOrganisationId))
                {
                    return routeOrganisationId;
                }
            }
        }

        var claimValue = context.User.FindFirstValue(OrganisationClaimTypes.OrganisationId);
        if (Guid.TryParse(claimValue, out var claimOrganisationId))
        {
            return claimOrganisationId;
        }

        return null;
    }
}
