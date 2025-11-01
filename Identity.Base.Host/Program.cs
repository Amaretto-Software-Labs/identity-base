using Identity.Base.Admin.Configuration;
using Identity.Base.Admin.Endpoints;
using Identity.Base.Email.MailJet;
using Identity.Base.Extensions;
using Identity.Base.Options;
using Identity.Base.Roles;
using Identity.Base.Roles.Endpoints;
using Identity.Base.Roles.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
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

var identityBuilder = builder.Services.AddIdentityBase(builder.Configuration, builder.Environment);

identityBuilder
    .AddGoogleAuth()
    .AddMicrosoftAuth()
    .AddAppleAuth()
    .UseMailJetEmailSender();

var rolesBuilder = builder.Services.AddIdentityAdmin(builder.Configuration);

rolesBuilder.AddDbContext<IdentityRolesDbContext>((provider, options) =>
{
    var databaseOptions = provider.GetRequiredService<IOptions<DatabaseOptions>>().Value;
    var connectionString = databaseOptions.Primary ?? string.Empty;

    if (string.IsNullOrWhiteSpace(connectionString))
    {
        throw new InvalidOperationException("ConnectionStrings:Primary must be provided.");
    }

    if (connectionString.StartsWith("InMemory:", StringComparison.OrdinalIgnoreCase))
    {
        var databaseName = connectionString[("InMemory:".Length)..];
        if (string.IsNullOrWhiteSpace(databaseName))
        {
            databaseName = "IdentityBaseTests";
        }

        options.UseInMemoryDatabase($"{databaseName}_roles");
    }
    else
    {
        options.UseNpgsql(
            connectionString,
            sql => sql.EnableRetryOnFailure());
    }
});

var app = builder.Build();

app.UseApiPipeline(appBuilder => appBuilder.UseSerilogRequestLogging());

app.MapControllers();
app.MapApiEndpoints();
app.MapIdentityAdminEndpoints();
app.MapIdentityRolesUserEndpoints();

await app.RunAsync();

public partial class Program;
