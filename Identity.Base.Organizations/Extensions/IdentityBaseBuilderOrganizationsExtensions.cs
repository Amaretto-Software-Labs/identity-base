using System;
using System.Threading;
using System.Threading.Tasks;
using Identity.Base.Extensions;
using Identity.Base.Identity;
using Identity.Base.Organizations.Abstractions;
using Identity.Base.Organizations.Services;
using Identity.Base.Options;
using Identity.Base.Roles.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Identity.Base.Organizations.Extensions;

public static class IdentityBaseBuilderOrganizationsExtensions
{
    public static IdentityBaseBuilder ConfigureOrganizationModel(
        this IdentityBaseBuilder builder,
        Action<ModelBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        builder.ModelCustomizationOptions.AddOrganizationDbContextCustomization(configure);
        return builder;
    }

    public static IdentityBaseBuilder AfterOrganizationSeed(
        this IdentityBaseBuilder builder,
        Func<IServiceProvider, CancellationToken, Task> callback)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(callback);

        builder.SeedCallbacks.RegisterOrganizationSeedCallback(callback);
        return builder;
    }

    public static IdentityBaseBuilder AddOrganizationClaimFormatter<TFormatter>(this IdentityBaseBuilder builder)
        where TFormatter : class, IPermissionClaimFormatter
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.Replace(ServiceDescriptor.Scoped<IPermissionClaimFormatter, TFormatter>());
        return builder;
    }

    public static IdentityBaseBuilder AddOrganizationScopeResolver<TResolver>(this IdentityBaseBuilder builder)
        where TResolver : class, IOrganizationScopeResolver
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.Replace(ServiceDescriptor.Scoped<IOrganizationScopeResolver, TResolver>());
        return builder;
    }
}
