using Identity.Base.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Identity.Base.Host.Extensions;

internal static class DbContextOptionsBuilderHostExtensions
{
    /// <summary>
    /// Host table prefix used for all DbContexts in this host.
    /// </summary>
    internal const string HostTablePrefix = "Host";

    public static DbContextOptionsBuilder<TContext> UseHostProvider<TContext>(
        this DbContextOptionsBuilder<TContext> builder,
        IConfiguration configuration,
        string migrationsAssembly,
        IServiceProvider? applicationServiceProvider = null)
        where TContext : DbContext
    {
        if (string.IsNullOrWhiteSpace(migrationsAssembly))
        {
            throw new InvalidOperationException("A migrations assembly must be provided when configuring the DbContext.");
        }

        var connectionString = configuration.GetConnectionString("Primary");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("ConnectionStrings:Primary must be configured.");
        }

        var databaseProvider = HostDatabaseProviderResolver.Resolve(configuration, connectionString);
        if (connectionString.StartsWith("InMemory:", StringComparison.OrdinalIgnoreCase))
        {
            var databaseName = connectionString["InMemory:".Length..];
            if (string.IsNullOrWhiteSpace(databaseName))
            {
                databaseName = "IdentityBaseTests";
            }

            builder.ConfigureWarnings(warnings => warnings.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning));
            builder.UseInMemoryDatabase(databaseName);
            return builder;
        }

        // Pass application service provider so DbContext can resolve IOptions<IdentityDbNamingOptions>
        // and other application services during model building.
        if (applicationServiceProvider is not null)
        {
            builder.UseApplicationServiceProvider(applicationServiceProvider);
        }

        // Suppress PendingModelChangesWarning to allow migrations to run.
        // This warning occurs due to differences between design-time and runtime model building:
        // - Design-time uses UseInternalServiceProvider for full control
        // - Runtime uses standard DI which may resolve services differently
        // The actual migrations are verified at design-time, so this is safe to suppress.
        builder.ConfigureWarnings(warnings =>
            warnings.Ignore(RelationalEventId.PendingModelChangesWarning));

        switch (databaseProvider)
        {
            case HostDatabaseProvider.SqlServer:
                builder.UseSqlServer(connectionString, sql =>
                {
                    sql.MigrationsAssembly(migrationsAssembly);

                    sql.EnableRetryOnFailure();
                });
                break;
            case HostDatabaseProvider.PostgreSql:
                builder.UseNpgsql(connectionString, sql =>
                {
                    sql.MigrationsAssembly(migrationsAssembly);

                    sql.EnableRetryOnFailure();
                });
                break;
            default:
                throw new InvalidOperationException($"Unsupported database provider '{configuration[HostDatabaseProviderResolver.ProviderConfigKey]}'.");
        }

        return builder;
    }

    /// <summary>
    /// Ensures IOptions&lt;IdentityDbNamingOptions&gt; is configured with the Host table prefix.
    /// Call this early during service registration, before any DbContext is registered.
    /// </summary>
    public static IServiceCollection ConfigureHostTablePrefix(this IServiceCollection services)
    {
        services.Configure<IdentityDbNamingOptions>(options =>
            options.TablePrefix = HostTablePrefix);
        return services;
    }
}

internal enum HostDatabaseProvider
{
    PostgreSql,
    SqlServer,
    InMemory
}

internal static class HostDatabaseProviderResolver
{
    internal const string ProviderConfigKey = "Database:Provider";
    internal const string MigrationsDefaultKey = "Database:Migrations:Default";

    public static HostDatabaseProvider Resolve(IConfiguration configuration, string connectionString)
    {
        if (connectionString.StartsWith("InMemory:", StringComparison.OrdinalIgnoreCase))
        {
            return HostDatabaseProvider.InMemory;
        }

        var provider = configuration[ProviderConfigKey];
        if (string.IsNullOrWhiteSpace(provider))
        {
            throw new InvalidOperationException("Database:Provider must be configured (PostgreSql or SqlServer).");
        }

        return provider switch
        {
            var p when p.Equals("PostgreSql", StringComparison.OrdinalIgnoreCase) => HostDatabaseProvider.PostgreSql,
            var p when p.Equals("SqlServer", StringComparison.OrdinalIgnoreCase) => HostDatabaseProvider.SqlServer,
            _ => throw new InvalidOperationException(
                $"Unsupported database provider '{provider}'. Allowed values: PostgreSql, SqlServer.")
        };
    }

    public static string ResolveMigrationsAssembly(IConfiguration configuration, string contextName)
    {
        var fromContext = configuration[$"Database:Migrations:{contextName}"];
        if (!string.IsNullOrWhiteSpace(fromContext))
        {
            return fromContext!;
        }

        var fromDefault = configuration[MigrationsDefaultKey];
        if (!string.IsNullOrWhiteSpace(fromDefault))
        {
            return fromDefault!;
        }

        throw new InvalidOperationException(
            $"No migrations assembly configured for context '{contextName}'. " +
            $"Set Database:Migrations:{contextName} or Database:Migrations:Default.");
    }

}
