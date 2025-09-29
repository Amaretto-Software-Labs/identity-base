using System.Security.Claims;

namespace Identity.Base.Admin.Authorization;

internal sealed class DefaultPermissionScopeResolver : IPermissionScopeResolver
{
    public bool IsInScope(ClaimsPrincipal user, string permission)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentException.ThrowIfNullOrEmpty(permission);
        return true;
    }
}
