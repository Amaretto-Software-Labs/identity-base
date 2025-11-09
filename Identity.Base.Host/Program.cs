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

var migrationsAssembly = typeof(Program).Assembly.FullName;
const string TablePrefix = "Host";

Action<IServiceProvider, DbContextOptionsBuilder> configureAppDbContext = (provider, options) =>
{
    var configuration = provider.GetRequiredService<IConfiguration>();
    if (options is DbContextOptionsBuilder<AppDbContext> typed)
    {
        typed.UseHostProvider(configuration, migrationsAssembly);
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
        typed.UseHostProvider(configuration, migrationsAssembly);
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
identityBuilder.UseTablePrefix(TablePrefix);

identityBuilder
    .AddGoogleAuth()
    .AddMicrosoftAuth()
    .AddAppleAuth()
    .UseMailJetEmailSender();

var rolesBuilder = builder.Services.AddIdentityAdmin(builder.Configuration, configureRolesDbContext);
rolesBuilder.UseTablePrefix(TablePrefix);

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
