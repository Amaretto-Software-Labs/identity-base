using System;
using Microsoft.AspNetCore.Authorization;

namespace Identity.Base.Organizations.Authorization;

public static class AuthorizationPolicyBuilderExtensions
{
    public static AuthorizationPolicyBuilder RequireOrganizationPermission(this AuthorizationPolicyBuilder builder, string permission)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.AddRequirements(new OrganizationPermissionRequirement(permission));
        return builder;
    }
}
