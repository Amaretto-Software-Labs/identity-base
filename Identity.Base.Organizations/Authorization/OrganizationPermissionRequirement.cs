using System;
using System.Linq;
using System.Threading.Tasks;
using Identity.Base.Roles.Claims;
using Microsoft.AspNetCore.Authorization;

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
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, OrganizationPermissionRequirement requirement)
    {
        if (context.User is null)
        {
            return Task.CompletedTask;
        }

        var hasPermission = context.User
            .FindAll(RoleClaimTypes.Permissions)
            .SelectMany(claim => claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Contains(requirement.Permission, StringComparer.OrdinalIgnoreCase);

        if (hasPermission)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
