using System.Security.Claims;

namespace Identity.Base.Admin.Authorization;

/// <summary>
/// Determines whether a permission requirement is satisfied within the current tenant or application scope.
/// </summary>
public interface IPermissionScopeResolver
{
    bool IsInScope(ClaimsPrincipal user, string permission);
}
