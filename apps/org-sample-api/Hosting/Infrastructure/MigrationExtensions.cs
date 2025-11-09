using Identity.Base.Data;
using Identity.Base.Organizations.Data;
using Identity.Base.Roles;
using Identity.Base.Roles.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;

namespace OrgSampleApi.Hosting.Infrastructure;

internal static class MigrationExtensions
{
    public static async Task ApplyOrgSampleMigrationsAsync(this WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var provider = scope.ServiceProvider;

        await MigrateAsync<AppDbContext>(provider);
        await MigrateAsync<IdentityRolesDbContext>(provider);
        await MigrateAsync<OrganizationDbContext>(provider);

        await provider.SeedIdentityRolesAsync();
    }

    private static async Task MigrateAsync<TContext>(IServiceProvider provider) where TContext : DbContext
    {
        var context = provider.GetService<TContext>();
        if (context is null)
        {
            return;
        }

        if (!context.Database.IsRelational())
        {
            return;
        }

        await context.Database.MigrateAsync();
    }
}
