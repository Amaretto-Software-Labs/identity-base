using Identity.Base.Roles.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Identity.Base.Roles.Configuration;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddIdentityRoles(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<RoleConfigurationOptions>()
            .Bind(configuration.GetSection(RoleConfigurationOptions.SectionName))
            .Validate(o => o.Definitions.All(d => !string.IsNullOrWhiteSpace(d.Name)), "Role definitions must include a name.")
            .ValidateOnStart();

        services.AddOptions<PermissionCatalogOptions>()
            .Bind(configuration.GetSection(PermissionCatalogOptions.SectionName))
            .Validate(o => o.Definitions.All(d => !string.IsNullOrWhiteSpace(d.Name)), "Permission definitions must include a name.")
            .ValidateOnStart();

        return services;
    }
}
