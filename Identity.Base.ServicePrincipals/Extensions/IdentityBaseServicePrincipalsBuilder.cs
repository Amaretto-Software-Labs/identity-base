using Identity.Base.ServicePrincipals.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Identity.Base.ServicePrincipals.Extensions;

public sealed class IdentityBaseServicePrincipalsBuilder(
    IServiceCollection services,
    ServicePrincipalModelOptions modelOptions)
{
    public IServiceCollection Services { get; } = services;

    public IdentityBaseServicePrincipalsBuilder ConfigureModel(Action<ModelBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        modelOptions.Customizations.Add(configure);
        return this;
    }

    public IdentityBaseServicePrincipalsBuilder UseDbContext<TContext>()
        where TContext : ServicePrincipalDbContext
    {
        if (typeof(TContext) != typeof(ServicePrincipalDbContext))
        {
            Services.AddScoped<ServicePrincipalDbContext>(provider => provider.GetRequiredService<TContext>());
        }
        return this;
    }
}

public sealed class ServicePrincipalModelOptions
{
    internal IList<Action<ModelBuilder>> Customizations { get; } = new List<Action<ModelBuilder>>();
}
