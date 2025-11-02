using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Identity.Base.Organizations.Data;

public sealed class OrganizationDbContextFactory : IDesignTimeDbContextFactory<OrganizationDbContext>
{
    public OrganizationDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration["IDENTITY_ORGANIZATIONS_CONNECTION"]
            ?? "Host=localhost;Port=5432;Database=identity_organizations_design;Username=postgres;Password=postgres";

        var builder = new DbContextOptionsBuilder<OrganizationDbContext>();
        builder.UseNpgsql(connectionString);

        return new OrganizationDbContext(builder.Options);
    }
}
