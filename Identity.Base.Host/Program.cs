using Identity.Base.Admin.Configuration;
using Identity.Base.Admin.Endpoints;
using Identity.Base.Data;
using Identity.Base.Email.MailJet;
using Identity.Base.Extensions;
using Identity.Base.Host;
using Identity.Base.Host.Extensions;
using Identity.Base.Roles;
using Identity.Base.Roles.Endpoints;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
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

var googleSection = builder.Configuration.GetSection("Authentication:Google");
var googleEnabled = googleSection.GetValue("Enabled", false);
if (googleEnabled)
{
    var provider = googleSection["Provider"] ?? "google";
    var scheme = googleSection["Scheme"] ?? "Google";
    var clientId = googleSection["ClientId"];
    var clientSecret = googleSection["ClientSecret"];
    var callbackPath = googleSection["CallbackPath"] ?? "/signin-google";

    if (string.IsNullOrWhiteSpace(clientId))
    {
        throw new InvalidOperationException("Authentication:Google:ClientId is required when Authentication:Google:Enabled is true.");
    }

    if (string.IsNullOrWhiteSpace(clientSecret))
    {
        throw new InvalidOperationException("Authentication:Google:ClientSecret is required when Authentication:Google:Enabled is true.");
    }

    identityBuilder.AddExternalAuthProvider(provider, scheme, auth => auth.AddGoogle(scheme, options =>
    {
        options.SignInScheme = IdentityConstants.ExternalScheme;
        options.ClientId = clientId;
        options.ClientSecret = clientSecret;
        options.CallbackPath = callbackPath;
    }));
}

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
