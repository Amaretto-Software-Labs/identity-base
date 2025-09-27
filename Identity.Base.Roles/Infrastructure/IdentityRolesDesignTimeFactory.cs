using Identity.Base.Roles.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Identity.Base.Roles.Infrastructure;

public sealed class IdentityRolesDesignTimeFactory : IDesignTimeDbContextFactory<IdentityRolesDbContext>
{
    public IdentityRolesDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration["IDENTITY_ROLES_CONNECTION"]
            ?? "Host=localhost;Port=5432;Database=identity_roles_design;Username=postgres;Password=postgres";

        var builder = new DbContextOptionsBuilder<IdentityRolesDbContext>();
        builder.UseNpgsql(connectionString);

        return new IdentityRolesDbContext(builder.Options);
    }
}
