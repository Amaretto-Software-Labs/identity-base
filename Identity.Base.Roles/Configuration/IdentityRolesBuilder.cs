using Identity.Base.Options;
using Identity.Base.Roles.Abstractions;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Identity.Base.Roles.Configuration;

public sealed class IdentityRolesBuilder
{
    internal IdentityRolesBuilder(IServiceCollection services)
    {
        Services = services;
    }

    public IServiceCollection Services { get; }

    /// <summary>
    /// Registers an existing DbContext type as the RBAC store. The DbContext must implement <see cref="IRoleDbContext"/> and already be registered.
    /// </summary>
    public IdentityRolesBuilder UseDbContext<TContext>() where TContext : class, IRoleDbContext
    {
        Services.AddScoped<IRoleDbContext, TContext>();
        return this;
    }

    /// <summary>
    /// Registers a DbContext for RBAC purposes with a configuration delegate.
    /// </summary>
    public IdentityRolesBuilder AddDbContext<TContext>(Action<IServiceProvider, DbContextOptionsBuilder> optionsAction)
        where TContext : DbContext, IRoleDbContext
    {
        ArgumentNullException.ThrowIfNull(optionsAction);

        Services.AddDbContext<TContext>((provider, options) =>
        {
            optionsAction(provider, options);
            TryAddCustomizationExtension(provider, options);
        });
        Services.AddScoped<IRoleDbContext, TContext>();
        return this;
    }

    public IdentityRolesBuilder AddDbContext<TContext>(Action<DbContextOptionsBuilder> optionsAction)
        where TContext : DbContext, IRoleDbContext
    {
        ArgumentNullException.ThrowIfNull(optionsAction);
        return AddDbContext<TContext>((_, builder) => optionsAction(builder));
    }

    private static void TryAddCustomizationExtension(IServiceProvider provider, DbContextOptionsBuilder options)
    {
        var customizationOptions = provider.GetService<IdentityBaseModelCustomizationOptions>();
        if (customizationOptions is null)
        {
            return;
        }

        ((IDbContextOptionsBuilderInfrastructure)options)
            .AddOrUpdateExtension(new IdentityBaseModelCustomizationOptionsExtension(customizationOptions));
    }
}
