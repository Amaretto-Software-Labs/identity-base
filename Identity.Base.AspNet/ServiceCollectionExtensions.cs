using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;

namespace Identity.Base.AspNet;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds JWT Bearer authentication configured for Identity.Base
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="authority">The Identity.Base authority URL (e.g., "https://localhost:5000")</param>
    /// <param name="audience">The API audience identifier (default: "identity.api")</param>
    /// <param name="configure">Optional configuration callback for JwtBearerOptions</param>
    public static IServiceCollection AddIdentityBaseAuthentication(
        this IServiceCollection services,
        string authority,
        string audience = "identity.api",
        Action<JwtBearerOptions>? configure = null)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = authority;
                options.Audience = audience;
                options.RequireHttpsMetadata = !authority.StartsWith("http://");

                // Configure HTTP client to bypass SSL validation for development
                if (authority.StartsWith("https://localhost"))
                {
                    options.BackchannelHttpHandler = new HttpClientHandler()
                    {
                        ServerCertificateCustomValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true
                    };
                }

                // Enable detailed logging for debugging
                options.Events = new JwtBearerEvents
                {
                    OnAuthenticationFailed = context =>
                    {
                        var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<JwtBearerHandler>>();
                        logger.LogError("JWT Authentication failed: {Error}", context.Exception?.Message);
                        return Task.CompletedTask;
                    },
                    OnTokenValidated = context =>
                    {
                        var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<JwtBearerHandler>>();
                        logger.LogDebug("JWT Token validated successfully for user: {User}",
                            context.Principal?.Identity?.Name ?? "Unknown");

                        // Log all claims for debugging
                        var claims = context.Principal?.Claims?.ToList() ?? new List<Claim>();
                        foreach (var claim in claims)
                        {
                            logger.LogTrace("JWT Claim: {Type} = {Value}", claim.Type, claim.Value);
                        }

                        return Task.CompletedTask;
                    }
                };

                // Allow additional configuration
                configure?.Invoke(options);
            });

        services.AddAuthorization();

        return services;
    }

    /// <summary>
    /// Adds request/response logging middleware for debugging authentication
    /// </summary>
    /// <param name="services">The service collection</param>
    public static IServiceCollection AddIdentityBaseLogging(this IServiceCollection services)
    {
        return services;
    }
}