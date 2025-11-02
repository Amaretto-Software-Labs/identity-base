using System;
using System.Threading;
using System.Threading.Tasks;
using Identity.Base.Extensions;
using Identity.Base.Identity;
using Identity.Base.Organisations.Abstractions;
using Identity.Base.Organisations.Services;
using Identity.Base.Options;
using Identity.Base.Roles.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Identity.Base.Organisations.Extensions;

public static class IdentityBaseBuilderOrganisationsExtensions
{
    public static IdentityBaseBuilder ConfigureOrganisationModel(
        this IdentityBaseBuilder builder,
        Action<ModelBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        builder.ModelCustomizationOptions.AddOrganisationDbContextCustomization(configure);
        return builder;
    }

    public static IdentityBaseBuilder AfterOrganisationSeed(
        this IdentityBaseBuilder builder,
        Func<IServiceProvider, CancellationToken, Task> callback)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(callback);

        builder.SeedCallbacks.RegisterOrganisationSeedCallback(callback);
        return builder;
    }

    public static IdentityBaseBuilder AddOrganisationClaimFormatter<TFormatter>(this IdentityBaseBuilder builder)
        where TFormatter : class, IPermissionClaimFormatter
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.Replace(ServiceDescriptor.Scoped<IPermissionClaimFormatter, TFormatter>());
        return builder;
    }

    public static IdentityBaseBuilder AddOrganisationScopeResolver<TResolver>(this IdentityBaseBuilder builder)
        where TResolver : class, IOrganisationScopeResolver
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.Replace(ServiceDescriptor.Scoped<IOrganisationScopeResolver, TResolver>());
        return builder;
    }
}
