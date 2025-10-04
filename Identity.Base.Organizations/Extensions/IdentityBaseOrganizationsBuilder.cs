using System;
using Identity.Base.Options;
using Identity.Base.Organizations.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Identity.Base.Organizations.Extensions;

public sealed class IdentityBaseOrganizationsBuilder
{
    internal IdentityBaseOrganizationsBuilder(IServiceCollection services)
    {
        Services = services;
    }

    public IServiceCollection Services { get; }

    public IdentityBaseOrganizationsBuilder UseDbContext<TContext>() where TContext : OrganizationDbContext
    {
        Services.AddScoped<OrganizationDbContext>(sp => sp.GetRequiredService<TContext>());
        return this;
    }

    public IdentityBaseOrganizationsBuilder AddDbContext<TContext>(Action<IServiceProvider, DbContextOptionsBuilder> configure)
        where TContext : OrganizationDbContext
    {
        ArgumentNullException.ThrowIfNull(configure);

        Services.AddDbContext<TContext>((provider, options) =>
        {
            configure(provider, options);
            TryAddCustomizationExtension(provider, options);
        });

        Services.AddScoped<OrganizationDbContext>(sp => sp.GetRequiredService<TContext>());
        return this;
    }

    public IdentityBaseOrganizationsBuilder AddDbContext<TContext>(Action<DbContextOptionsBuilder> configure)
        where TContext : OrganizationDbContext
    {
        ArgumentNullException.ThrowIfNull(configure);
        return AddDbContext<TContext>((_, options) => configure(options));
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
