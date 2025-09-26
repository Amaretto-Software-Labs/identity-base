using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Identity.Base.AspNet;

public static class ApplicationBuilderExtensions
{
    /// <summary>
    /// Adds request logging middleware for debugging authentication
    /// </summary>
    /// <param name="app">The application builder</param>
    /// <param name="enableDetailedLogging">Enable detailed JWT token logging (default: false for security)</param>
    public static IApplicationBuilder UseIdentityBaseRequestLogging(this IApplicationBuilder app, bool enableDetailedLogging = false)
    {
        return app.Use(async (context, next) =>
        {
            var logger = context.RequestServices.GetRequiredService<ILogger<IdentityBaseMiddleware>>();

            var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
            var authHeaderDisplay = "None";

            if (authHeader != null && authHeader.StartsWith("Bearer "))
            {
                var token = authHeader.Substring("Bearer ".Length);
                if (enableDetailedLogging)
                {
                    authHeaderDisplay = $"Bearer {token[..Math.Min(20, token.Length)]}...";
                }
                else
                {
                    authHeaderDisplay = "Bearer [REDACTED]";
                }
            }

            logger.LogDebug("Request: {Method} {Path} - Auth Header: {AuthHeader}",
                context.Request.Method,
                context.Request.Path,
                authHeaderDisplay);

            await next();

            logger.LogDebug("Response: {StatusCode} - User authenticated: {IsAuthenticated} - User: {User}",
                context.Response.StatusCode,
                context.User?.Identity?.IsAuthenticated ?? false,
                context.User?.Identity?.Name ?? "None");
        });
    }

    /// <summary>
    /// Configures the standard authentication and authorization middleware for Identity.Base
    /// </summary>
    /// <param name="app">The application builder</param>
    public static IApplicationBuilder UseIdentityBaseAuthentication(this IApplicationBuilder app)
    {
        app.UseAuthentication();
        app.UseAuthorization();
        return app;
    }
}

// Internal class for logging purposes
internal class IdentityBaseMiddleware
{
}