using System.Security.Claims;
using Identity.Base.Admin.Options;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace Identity.Base.Admin.Authorization;

public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly AdminApiOptions _options;
    private readonly IPermissionScopeResolver _scopeResolver;

    public PermissionAuthorizationHandler(
        IOptions<AdminApiOptions> options,
        IPermissionScopeResolver scopeResolver)
    {
        _options = options.Value;
        _scopeResolver = scopeResolver;
    }

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

        if (!_scopeResolver.IsInScope(context.User, requirement.Permission))
        {
            return Task.CompletedTask;
        }

        if (HasPermission(context.User, requirement.Permission))
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

    private static bool HasPermission(ClaimsPrincipal user, string permission)
    {
        return user.FindAll(AdminClaimTypes.Permissions)
            .SelectMany(claim => claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Contains(permission, StringComparer.OrdinalIgnoreCase);
    }
}
