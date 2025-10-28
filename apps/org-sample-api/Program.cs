using OrgSampleApi.Hosting;
using OrgSampleApi.Hosting.Configuration;

var builder = WebApplication.CreateBuilder(args);
builder.AddOrgSampleServices();

var app = builder.Build();
var logger = app.UseOrgSampleLifecycleLogging();

app.ConfigureOrgSamplePipeline();

app.Run();

logger.LogInformation("Shut down");
