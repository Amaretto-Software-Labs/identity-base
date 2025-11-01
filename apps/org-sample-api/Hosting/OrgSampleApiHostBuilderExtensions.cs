using Identity.Base.Extensions;
using Identity.Base.Organizations.Extensions;
using Identity.Base.Roles.Endpoints;
using Identity.Base.Admin.Endpoints;
using Microsoft.AspNetCore.Builder;
using OrgSampleApi.Hosting.Configuration;
using OrgSampleApi.Hosting.Endpoints;
using Serilog;

namespace OrgSampleApi.Hosting;

internal static class OrgSampleApiHostBuilderExtensions
{
    public static void AddOrgSampleServices(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.AddOrgSampleLogging();
        builder.Services.AddOrgSampleOptions(builder.Configuration);
        builder.Services.AddOrgSampleCoreServices();
        builder.Services.ConfigureIdentityBase(builder.Configuration, builder.Environment);
    }

    public static void ConfigureOrgSamplePipeline(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.UseApiPipeline(appBuilder => appBuilder.UseSerilogRequestLogging());

        app.MapControllers();
        app.MapApiEndpoints();
        app.MapIdentityAdminEndpoints();
        app.MapIdentityRolesUserEndpoints();
        app.MapIdentityBaseOrganizationEndpoints();
        app.MapOrgSampleEndpoints();
    }
}
