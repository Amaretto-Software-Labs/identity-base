using Identity.Base.Host.Extensions;
using Identity.Base.Options;
using Identity.Base.Roles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Identity.Base.Host.Data.DesignTime;

internal sealed class HostRolesDbContextFactory : IDesignTimeDbContextFactory<IdentityRolesDbContext>
{
    private const string TablePrefix = "Host";

    public IdentityRolesDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var services = new ServiceCollection();
        services.AddEntityFrameworkNpgsql();
        services.AddSingleton<IOptions<IdentityDbNamingOptions>>(Microsoft.Extensions.Options.Options.Create(new IdentityDbNamingOptions
        {
            TablePrefix = TablePrefix
        }));
        var provider = services.BuildServiceProvider();

        var builder = new DbContextOptionsBuilder<IdentityRolesDbContext>()
            .UseInternalServiceProvider(provider)
            .UseHostProvider(configuration, typeof(Program).Assembly.FullName);

        return new IdentityRolesDbContext(builder.Options);
    }
}
