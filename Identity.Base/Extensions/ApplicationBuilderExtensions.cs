using Identity.Base.Options;
using Microsoft.AspNetCore.Builder;
using Serilog;
using Serilog.Context;
using System.Security.Claims;

namespace Identity.Base.Extensions;

public static class ApplicationBuilderExtensions
{
    public static WebApplication UseApiPipeline(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
        }

        app.UseHttpsRedirection();
        app.UseSerilogRequestLogging();
        app.Use(async (context, next) =>
        {
            var correlationId = context.TraceIdentifier;
            var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);

            using (LogContext.PushProperty("CorrelationId", correlationId))
            using (LogContext.PushProperty("UserId", userId ?? "anonymous"))
            {
                await next().ConfigureAwait(false);
            }
        });
        app.UseCors(CorsSettings.PolicyName);
        app.UseAuthentication();
        app.UseAuthorization();

        return app;
    }
}
