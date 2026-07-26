using System.Security.Claims;
using Identity.Base.Roles.Claims;
using Identity.Base.Organizations.Services;
using Identity.Base.Organizations.Claims;
using Identity.Base.Organizations.Options;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using System.Globalization;
using Microsoft.Extensions.Options;

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

public sealed class OrganizationPermissionAuthorizationHandler(
    IOrganizationPermissionResolver permissionResolver,
    IOptions<OrganizationAuthorizationOptions> authorizationOptions) : AuthorizationHandler<OrganizationPermissionRequirement>
{
    private readonly IOrganizationPermissionResolver _permissionResolver = permissionResolver ?? throw new ArgumentNullException(nameof(permissionResolver));
    private readonly OrganizationAuthorizationOptions _authorizationOptions = authorizationOptions?.Value ?? throw new ArgumentNullException(nameof(authorizationOptions));
    private const string ScopeClaimType = "scope";

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, OrganizationPermissionRequirement requirement)
    {
        if (context.User is null)
        {
            return;
        }

        if (IsAdminPermission(requirement.Permission) && !HasAdminScope(context.User))
        {
            return;
        }

        var isUserOrganizationPermission = requirement.Permission.StartsWith(
            "user.organizations.",
            StringComparison.OrdinalIgnoreCase);
        var routeOrganizationId = ResolveRouteOrganizationId(context);

        if ((!isUserOrganizationPermission || !routeOrganizationId.HasValue) &&
            context.User.HasPermission(requirement.Permission))
        {
            context.Succeed(requirement);
            return;
        }

        if (!isUserOrganizationPermission)
        {
            return;
        }

        var organizationId = routeOrganizationId ?? ResolveClaimOrganizationId(context.User);
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

    private static Guid? ResolveRouteOrganizationId(AuthorizationHandlerContext context)
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

        return null;
    }

    private static Guid? ResolveClaimOrganizationId(ClaimsPrincipal user)
    {
        var claimValue = user.FindFirstValue(OrganizationClaimTypes.OrganizationId);
        if (Guid.TryParse(claimValue, out var claimOrganizationId))
        {
            return claimOrganizationId;
        }

        return null;
    }

    private static bool IsAdminPermission(string permission)
        => permission.StartsWith("admin.organizations.", StringComparison.OrdinalIgnoreCase);

    private bool HasAdminScope(ClaimsPrincipal user)
    {
        if (string.IsNullOrWhiteSpace(_authorizationOptions.AdminRequiredScope))
        {
            return true;
        }

        var scopes = user.FindAll(ScopeClaimType)
            .SelectMany(claim => claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        return scopes.Contains(_authorizationOptions.AdminRequiredScope, StringComparer.Ordinal);
    }
}
