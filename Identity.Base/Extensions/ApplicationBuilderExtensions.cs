using System;
using System.Collections.Generic;
using System.Security.Claims;
using Identity.Base.Options;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Identity.Base.Extensions;

public static class ApplicationBuilderExtensions
{
    public static WebApplication UseApiPipeline(this WebApplication app, Action<WebApplication>? configureLogging = null)
    {
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
        }

        configureLogging?.Invoke(app);

        var scopeLogger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Identity.Base.RequestScope");

        app.Use(async (context, next) =>
        {
            var correlationId = context.TraceIdentifier;
            var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous";

            using (scopeLogger.BeginScope(new Dictionary<string, object?>
                   {
                       ["CorrelationId"] = correlationId,
                       ["UserId"] = userId,
                   }))
            {
                context.Items["IdentityBase:CorrelationId"] = correlationId;
                await next().ConfigureAwait(false);
            }
        });

        app.UseCors(CorsSettings.PolicyName);
        app.UseAuthentication();
        app.UseAuthorization();

        return app;
    }
}
