using OrgSampleApi.Hosting;
using OrgSampleApi.Hosting.Configuration;
using OrgSampleApi.Hosting.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
builder.AddOrgSampleServices();

var app = builder.Build();
var logger = app.UseOrgSampleLifecycleLogging();

app.ConfigureOrgSamplePipeline();

await app.ApplyOrgSampleMigrationsAsync();

app.Run();

logger.LogInformation("Shut down");

public partial class Program;
