using Identity.Base.Options;
using Identity.Base.Organizations.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrgSampleApi.Hosting.Infrastructure;

namespace OrgSampleApi.Hosting.Infrastructure.DesignTime;

internal sealed class OrgSampleOrganizationDbContextFactory : IDesignTimeDbContextFactory<OrganizationDbContext>
{
    private const string TablePrefix = "OrgSample";

    public OrganizationDbContext CreateDbContext(string[] args)
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

        var builder = new DbContextOptionsBuilder<OrganizationDbContext>()
            .UseInternalServiceProvider(provider)
            .UseOrgSampleProvider(configuration, typeof(Program).Assembly.FullName);

        return new OrganizationDbContext(builder.Options);
    }
}
