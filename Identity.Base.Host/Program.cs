using Identity.Base.Extensions;
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
    .AddAppleAuth();

var app = builder.Build();

app.UseApiPipeline();

app.MapControllers();
app.MapApiEndpoints();

app.Run();

public partial class Program;
