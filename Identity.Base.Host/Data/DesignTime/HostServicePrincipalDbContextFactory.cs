using Identity.Base.Host.Extensions;
using Identity.Base.Options;
using Identity.Base.ServicePrincipals.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Identity.Base.Host.Data.DesignTime;

internal sealed class HostServicePrincipalDbContextFactory : IDesignTimeDbContextFactory<ServicePrincipalDbContext>
{
    public ServicePrincipalDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();
        var connectionString = configuration.GetConnectionString("Primary")
            ?? throw new InvalidOperationException("ConnectionStrings:Primary must be configured.");
        var migrationsAssembly = HostDatabaseProviderResolver.ResolveMigrationsAssembly(
            configuration, nameof(ServicePrincipalDbContext));
        var builder = new DbContextOptionsBuilder<ServicePrincipalDbContext>()
            .UseHostProvider(configuration, migrationsAssembly);
        return new ServicePrincipalDbContext(
            builder.Options,
            Microsoft.Extensions.Options.Options.Create(new IdentityDbNamingOptions { TablePrefix = "Host" }));
    }
}
