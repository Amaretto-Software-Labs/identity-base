using Identity.Base.Abstractions;
using Identity.Base.Roles.Infrastructure;
using Identity.Base.Roles.Options;
using Identity.Base.Roles.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Identity.Base.Roles.Configuration;

public static class ServiceCollectionExtensions
{
    public static IdentityRolesBuilder AddIdentityRoles(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<RoleConfigurationOptions>()
            .Bind(configuration.GetSection(RoleConfigurationOptions.SectionName))
            .Validate(o => o.Definitions.All(d => !string.IsNullOrWhiteSpace(d.Name)), "Role definitions must include a name.")
            .ValidateOnStart();

        services.AddOptions<PermissionCatalogOptions>()
            .Bind(configuration.GetSection(PermissionCatalogOptions.SectionName))
            .Validate(o => o.Definitions.All(d => !string.IsNullOrWhiteSpace(d.Name)), "Permission definitions must include a name.")
            .ValidateOnStart();

        services.TryAddScoped<IRoleSeeder, RoleSeeder>();
        services.TryAddScoped<IRoleAssignmentService, RoleAssignmentService>();
        services.TryAddScoped<IPermissionResolver, RoleAssignmentService>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IUserCreationListener, DefaultUserRoleAssignmentListener>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IClaimsPrincipalAugmentor, PermissionClaimsAugmentor>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, IdentityRolesSeedHostedService>());

        return new IdentityRolesBuilder(services);
    }

    public static async Task SeedIdentityRolesAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var seeder = scope.ServiceProvider.GetService<IRoleSeeder>();
        if (seeder is null)
        {
            return;
        }

        await seeder.SeedAsync(cancellationToken).ConfigureAwait(false);
    }
}
