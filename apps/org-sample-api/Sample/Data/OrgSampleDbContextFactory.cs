using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace OrgSampleApi.Sample.Data;

internal sealed class OrgSampleDbContextFactory : IDesignTimeDbContextFactory<OrgSampleDbContext>
{
    public OrgSampleDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("Primary")
            ?? "Host=localhost;Port=5432;Database=identity_org_sample_design;Username=postgres;Password=postgres";

        var builder = new DbContextOptionsBuilder<OrgSampleDbContext>();
        builder.UseNpgsql(connectionString);

        return new OrgSampleDbContext(builder.Options);
    }
}
