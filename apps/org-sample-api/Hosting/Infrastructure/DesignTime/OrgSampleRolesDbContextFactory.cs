using Identity.Base.Options;
using Identity.Base.Roles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrgSampleApi.Hosting.Infrastructure;

namespace OrgSampleApi.Hosting.Infrastructure.DesignTime;

internal sealed class OrgSampleRolesDbContextFactory : IDesignTimeDbContextFactory<IdentityRolesDbContext>
{
    private const string TablePrefix = "OrgSample";

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
            .UseOrgSampleProvider(configuration, typeof(Program).Assembly.FullName);

        return new IdentityRolesDbContext(builder.Options);
    }
}
