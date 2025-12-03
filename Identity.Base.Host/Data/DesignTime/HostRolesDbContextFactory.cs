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

        var connectionString = configuration.GetConnectionString("Primary")
            ?? throw new InvalidOperationException("ConnectionStrings:Primary must be configured.");
        var databaseProvider = HostDatabaseProviderResolver.Resolve(configuration, connectionString);
        var migrationsAssembly = HostDatabaseProviderResolver.ResolveMigrationsAssembly(
            configuration,
            nameof(IdentityRolesDbContext));

        var services = new ServiceCollection();
        services.AddSingleton<IOptions<IdentityDbNamingOptions>>(Microsoft.Extensions.Options.Options.Create(new IdentityDbNamingOptions
        {
            TablePrefix = TablePrefix
        }));
        switch (databaseProvider)
        {
            case HostDatabaseProvider.SqlServer:
                services.AddEntityFrameworkSqlServer();
                break;
            case HostDatabaseProvider.PostgreSql:
                services.AddEntityFrameworkNpgsql();
                break;
            case HostDatabaseProvider.InMemory:
                services.AddEntityFrameworkInMemoryDatabase();
                break;
            default:
                throw new InvalidOperationException($"Unsupported database provider '{databaseProvider}'.");
        }
        var serviceProvider = services.BuildServiceProvider();

        var builder = new DbContextOptionsBuilder<IdentityRolesDbContext>()
            .UseInternalServiceProvider(serviceProvider)
            .UseHostProvider(configuration, migrationsAssembly);

        return new IdentityRolesDbContext(builder.Options);
    }
}
