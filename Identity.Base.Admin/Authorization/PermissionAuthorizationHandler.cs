using System.Security.Claims;
using Identity.Base.Admin.Options;
using Identity.Base.Roles.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace Identity.Base.Admin.Authorization;

public sealed class PermissionAuthorizationHandler(
    IOptions<AdminApiOptions> options,
    IPermissionScopeResolver scopeResolver) : AuthorizationHandler<PermissionRequirement>
{
    private readonly AdminApiOptions _options = options.Value;

    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (context.User is null)
        {
            return Task.CompletedTask;
        }

        if (!HasRequiredScope(context.User))
        {
            return Task.CompletedTask;
        }

        if (!scopeResolver.IsInScope(context.User, requirement.Permission))
        {
            return Task.CompletedTask;
        }

        if (context.User.HasPermission(requirement.Permission))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }

    private bool HasRequiredScope(ClaimsPrincipal user)
    {
        if (string.IsNullOrWhiteSpace(_options.RequiredScope))
        {
            return true;
        }

        var scopes = user.FindAll(AdminClaimTypes.Scope)
            .SelectMany(claim => claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries));

        return scopes.Contains(_options.RequiredScope, StringComparer.Ordinal);
    }

}
