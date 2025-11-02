using System;
using Microsoft.AspNetCore.Authorization;

namespace Identity.Base.Organisations.Authorization;

public static class AuthorizationPolicyBuilderExtensions
{
    public static AuthorizationPolicyBuilder RequireOrganisationPermission(this AuthorizationPolicyBuilder builder, string permission)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.AddRequirements(new OrganisationPermissionRequirement(permission));
        return builder;
    }
}
