using System;
using Identity.Base.Options;
using Identity.Base.Organisations.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Identity.Base.Organisations.Extensions;

public sealed class IdentityBaseOrganisationsBuilder
{
    internal IdentityBaseOrganisationsBuilder(IServiceCollection services)
    {
        Services = services;
    }

    public IServiceCollection Services { get; }

    public IdentityBaseOrganisationsBuilder UseDbContext<TContext>() where TContext : OrganisationDbContext
    {
        if (typeof(TContext) != typeof(OrganisationDbContext))
        {
            Services.AddScoped<OrganisationDbContext>(sp => sp.GetRequiredService<TContext>());
        }

        return this;
    }

    public IdentityBaseOrganisationsBuilder AddDbContext<TContext>(Action<IServiceProvider, DbContextOptionsBuilder> configure)
        where TContext : OrganisationDbContext
    {
        ArgumentNullException.ThrowIfNull(configure);

        Services.AddDbContext<TContext>((provider, options) =>
        {
            configure(provider, options);
            TryAddCustomizationExtension(provider, options);
        });

        if (typeof(TContext) != typeof(OrganisationDbContext))
        {
            Services.AddScoped<OrganisationDbContext>(sp => sp.GetRequiredService<TContext>());
        }

        return this;
    }

    public IdentityBaseOrganisationsBuilder AddDbContext<TContext>(Action<DbContextOptionsBuilder> configure)
        where TContext : OrganisationDbContext
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
