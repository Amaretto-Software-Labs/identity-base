using Identity.Base.Features.Authentication;
using Identity.Base.Features.Authentication.Authorize;
using Identity.Base.Features.Email;
using Identity.Base.Features.Users;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Linq;
using System.Text.Json;

namespace Identity.Base.Extensions;

public static class EndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapApiEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHealthChecks("/healthz", new HealthCheckOptions
            {
                ResponseWriter = async (context, report) =>
                {
                    context.Response.ContentType = "application/json";

                    var payload = new
                    {
                        status = report.Status.ToString(),
                        checks = report.Entries.Select(
                            entry => new
                            {
                                name = entry.Key,
                                status = entry.Value.Status.ToString(),
                                durationMs = entry.Value.Duration.TotalMilliseconds
                            })
                    };

                    await context.Response.WriteAsync(JsonSerializer.Serialize(payload));
                }
            })
            .WithName("Healthz")
            .WithTags("Diagnostics");

        endpoints.MapAuthenticationEndpoints();
        endpoints.MapAuthorizeEndpoint();
        endpoints.MapUserEndpoints();
        endpoints.MapEmailEndpoints();

        return endpoints;
    }
}
