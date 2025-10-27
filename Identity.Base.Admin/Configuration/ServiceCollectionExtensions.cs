using Identity.Base.Admin.Authorization;
using Identity.Base.Admin.Options;
using Identity.Base.Roles.Configuration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Identity.Base.Admin.Configuration;

public static class ServiceCollectionExtensions
{
    public static IdentityRolesBuilder AddIdentityAdmin(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AdminApiOptions>(configuration.GetSection(AdminApiOptions.SectionName));
        services.Configure<AdminDiagnosticsOptions>(configuration.GetSection(AdminDiagnosticsOptions.SectionName));

        var rolesBuilder = services.AddIdentityRoles(configuration);

        services.TryAddSingleton<IPermissionScopeResolver, DefaultPermissionScopeResolver>();
        services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
        return rolesBuilder;
    }

    public static AuthorizationPolicyBuilder RequireAdminPermission(this AuthorizationPolicyBuilder builder, string permission)
    {
        builder.AddRequirements(new PermissionRequirement(permission));
        return builder;
    }
}
