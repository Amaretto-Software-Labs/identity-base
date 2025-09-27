using Identity.Base.Roles.Claims;

namespace Identity.Base.Admin.Authorization;

public static class AdminClaimTypes
{
    public const string Scope = "scope"; // OAuth2 scope claim in JWT
    public const string Permissions = RoleClaimTypes.Permissions; // custom permission claim populated from RBAC
}
