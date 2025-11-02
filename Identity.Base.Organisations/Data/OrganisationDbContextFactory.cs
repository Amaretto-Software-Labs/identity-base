using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Identity.Base.Organisations.Data;

public sealed class OrganisationDbContextFactory : IDesignTimeDbContextFactory<OrganisationDbContext>
{
    public OrganisationDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration["IDENTITY_ORGANISATIONS_CONNECTION"]
            ?? "Host=localhost;Port=5432;Database=identity_organisations_design;Username=postgres;Password=postgres";

        var builder = new DbContextOptionsBuilder<OrganisationDbContext>();
        builder.UseNpgsql(connectionString);

        return new OrganisationDbContext(builder.Options);
    }
}
