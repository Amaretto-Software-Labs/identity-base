using Identity.Base.OpenIddict;
using Identity.Base.Roles.Options;
using Identity.Base.ServicePrincipals.Data;
using Identity.Base.ServicePrincipals.Domain;
using Identity.Base.ServicePrincipals.OpenIddict;
using Identity.Base.ServicePrincipals.Options;
using Identity.Base.ServicePrincipals.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenIddict.Server;
using OpenIddict.Validation;

namespace Identity.Base.ServicePrincipals.Extensions;

public static class ServiceCollectionExtensions
{
    public static IdentityBaseServicePrincipalsBuilder AddIdentityBaseServicePrincipals(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<IServiceProvider, DbContextOptionsBuilder>? configureDbContext = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<ServicePrincipalOptions>()
            .Bind(configuration.GetSection(ServicePrincipalOptions.SectionName))
            .Validate(options => options.AccessTokenLifetime > TimeSpan.Zero,
                "Service principal access token lifetime must be positive.")
            .ValidateOnStart();

        var modelOptions = new ServicePrincipalModelOptions();
        services.TryAddSingleton(modelOptions);
        services.TryAddScoped<ServicePrincipalService>();
        services.TryAddScoped<IPasswordHasher<ServicePrincipalCredential>, PasswordHasher<ServicePrincipalCredential>>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IClientCredentialsPrincipalProvider, ServicePrincipalPrincipalProvider>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IManagedClientCredentialsClientResolver, ServicePrincipalPrincipalProvider>());
        services.PostConfigure<PermissionCatalogOptions>(AddPermissionDefinitions);
        services.Configure<OpenIddictValidationOptions>(options => options.EnableTokenEntryValidation = true);

        services.AddOpenIddict().AddServer(options =>
        {
            options.RemoveEventHandler(OpenIddictServerHandlers.ValidateClientSecret.Descriptor);
            options.AddEventHandler(ServicePrincipalClientSecretValidator.Descriptor);
        });

        if (configureDbContext is not null)
        {
            services.AddDbContext<ServicePrincipalDbContext>(configureDbContext);
        }

        return new IdentityBaseServicePrincipalsBuilder(services, modelOptions);
    }

    private static void AddPermissionDefinitions(PermissionCatalogOptions options)
    {
        foreach (var permission in ServicePrincipalPermissions.All)
        {
            if (!options.Definitions.Any(item => string.Equals(item.Name, permission, StringComparison.OrdinalIgnoreCase)))
            {
                options.Definitions.Add(new PermissionDefinition
                {
                    Name = permission,
                    Description = $"Admin permission for {permission}."
                });
            }
        }
    }
}
