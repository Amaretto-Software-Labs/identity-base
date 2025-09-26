using System;
using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Identity.Base.Data;

public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var configuration = BuildConfiguration(args);
        var connectionString = ResolveConnectionString(configuration);

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();

        if (connectionString.StartsWith("InMemory:", StringComparison.OrdinalIgnoreCase))
        {
            var databaseName = connectionString.Substring("InMemory:".Length);
            if (string.IsNullOrWhiteSpace(databaseName))
            {
                databaseName = "IdentityBaseDesignTime";
            }

            optionsBuilder.UseInMemoryDatabase(databaseName);
        }
        else
        {
            optionsBuilder.UseNpgsql(connectionString, builder => builder.EnableRetryOnFailure());
        }

        return new AppDbContext(optionsBuilder.Options);
    }

    private static IConfiguration BuildConfiguration(string[] args)
        => new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables()
            .AddCommandLine(args)
            .Build();

    private static string ResolveConnectionString(IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Primary")
            ?? configuration["ConnectionStrings:Primary"];

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return "InMemory:IdentityBaseDesignTime";
        }

        return connectionString;
    }
}
