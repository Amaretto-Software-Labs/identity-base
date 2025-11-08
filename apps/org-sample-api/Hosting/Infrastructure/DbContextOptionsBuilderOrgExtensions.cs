using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;

namespace OrgSampleApi.Hosting.Infrastructure;

internal static class DbContextOptionsBuilderOrgExtensions
{
    public static DbContextOptionsBuilder<TContext> UseOrgSampleProvider<TContext>(
        this DbContextOptionsBuilder<TContext> builder,
        IConfiguration configuration,
        string? migrationsAssembly = null)
        where TContext : DbContext
    {
        var connectionString = configuration.GetConnectionString("Primary");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("ConnectionStrings:Primary must be configured.");
        }

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

        builder.UseNpgsql(connectionString, sql =>
        {
            if (!string.IsNullOrWhiteSpace(migrationsAssembly))
            {
                sql.MigrationsAssembly(migrationsAssembly);
            }

            sql.EnableRetryOnFailure();
        });

        return builder;
    }
}
