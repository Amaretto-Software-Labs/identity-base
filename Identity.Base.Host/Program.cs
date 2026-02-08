using Identity.Base.Admin.Configuration;
using Identity.Base.Admin.Endpoints;
using Identity.Base.Data;
using Identity.Base.Email.MailJet;
using Identity.Base.Extensions;
using Identity.Base.Host.Extensions;
using Identity.Base.Organizations.Data;
using Identity.Base.Roles;
using Identity.Base.Roles.Configuration;
using Identity.Base.Roles.Endpoints;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, loggerConfiguration) =>
{
    loggerConfiguration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .Enrich.WithMachineName()
        .Enrich.WithEnvironmentName()
        .WriteTo.Console();
});

// Configure the table prefix FIRST, before any DbContext registration.
// This ensures IOptions<IdentityDbNamingOptions> is available with the correct prefix.
builder.Services.ConfigureHostTablePrefix();

var migrationsAssemblyApp = HostDatabaseProviderResolver.ResolveMigrationsAssembly(
    builder.Configuration,
    nameof(AppDbContext));
var migrationsAssemblyRoles = HostDatabaseProviderResolver.ResolveMigrationsAssembly(
    builder.Configuration,
    nameof(IdentityRolesDbContext));

Action<IServiceProvider, DbContextOptionsBuilder> configureAppDbContext = (provider, options) =>
{
    var configuration = provider.GetRequiredService<IConfiguration>();
    if (options is DbContextOptionsBuilder<AppDbContext> typed)
    {
        typed.UseHostProvider(configuration, migrationsAssemblyApp, provider);
    }
    else
    {
        throw new InvalidOperationException("Unable to configure AppDbContext options.");
    }
};

Action<IServiceProvider, DbContextOptionsBuilder> configureRolesDbContext = (provider, options) =>
{
    var configuration = provider.GetRequiredService<IConfiguration>();
    if (options is DbContextOptionsBuilder<IdentityRolesDbContext> typed)
    {
        typed.UseHostProvider(configuration, migrationsAssemblyRoles, provider);
    }
    else
    {
        throw new InvalidOperationException("Unable to configure IdentityRolesDbContext options.");
    }
};

var identityBuilder = builder.Services.AddIdentityBase(
    builder.Configuration,
    builder.Environment,
    configureDbContext: configureAppDbContext);

// Table prefix is configured globally via ConfigureHostTablePrefix() above

identityBuilder
    .UseMailJetEmailSender();

builder.Services.AddIdentityAdmin(builder.Configuration, configureRolesDbContext);

var app = builder.Build();

app.UseApiPipeline(appBuilder => appBuilder.UseSerilogRequestLogging());

app.MapControllers();
app.MapApiEndpoints();
app.MapIdentityAdminEndpoints();
app.MapIdentityRolesUserEndpoints();

await HostMigrationRunner.ApplyMigrationsAsync(app.Services);

await app.RunAsync();

public partial class Program;

internal static class HostMigrationRunner
{
    public static async Task ApplyMigrationsAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
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
