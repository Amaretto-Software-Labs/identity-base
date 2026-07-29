using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using Identity.Base.Options;

namespace Identity.Base.Features.Authentication.Passkeys;

public static class PasskeyRateLimitPolicies
{
    public const string Configuration = "passkeys-configuration";
    public const string AuthenticationOptions = "passkeys-authentication-options";
    public const string Authentication = "passkeys-authentication";
    public const string SignupEnrollment = "passkeys-signup-enrollment";
    public const string SignupEmail = "passkeys-signup-email";
    public const string RecoveryEnrollment = "passkeys-recovery-enrollment";
    public const string RecoveryEmail = "passkeys-recovery-email";
    public const string Creation = "passkeys-creation";
    public const string Management = "passkeys-management";
    public const string Admin = "passkeys-admin";

    public static IServiceCollection AddPasskeyRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(Configuration, context =>
            {
                var rateLimits = ResolveRateLimits(context);
                return PerIp(context, rateLimits.Configuration, rateLimits.Enabled);
            });
            options.AddPolicy(AuthenticationOptions, context =>
            {
                var rateLimits = ResolveRateLimits(context);
                return PerIp(context, rateLimits.AuthenticationOptions, rateLimits.Enabled);
            });
            options.AddPolicy(Authentication, context =>
            {
                var rateLimits = ResolveRateLimits(context);
                return PerIp(context, rateLimits.Authentication, rateLimits.Enabled);
            });
            options.AddPolicy(SignupEnrollment, context =>
            {
                var rateLimits = ResolveRateLimits(context);
                return PerIp(context, rateLimits.SignupEnrollment, rateLimits.Enabled);
            });
            options.AddPolicy(SignupEmail, context =>
            {
                var rateLimits = ResolveRateLimits(context);
                return PerIp(context, rateLimits.SignupEmail, rateLimits.Enabled);
            });
            options.AddPolicy(RecoveryEnrollment, context =>
            {
                var rateLimits = ResolveRateLimits(context);
                return PerIp(context, rateLimits.RecoveryEnrollment, rateLimits.Enabled);
            });
            options.AddPolicy(RecoveryEmail, context =>
            {
                var rateLimits = ResolveRateLimits(context);
                return PerIp(context, rateLimits.RecoveryEmail, rateLimits.Enabled);
            });
            options.AddPolicy(Creation, context =>
            {
                var rateLimits = ResolveRateLimits(context);
                return PerActorOrIp(context, rateLimits.Creation, rateLimits.Enabled);
            });
            options.AddPolicy(Management, context =>
            {
                var rateLimits = ResolveRateLimits(context);
                return PerActorOrIp(context, rateLimits.Management, rateLimits.Enabled);
            });
            options.AddPolicy(Admin, context =>
            {
                var rateLimits = ResolveRateLimits(context);
                return PerActorOrIp(context, rateLimits.Admin, rateLimits.Enabled);
            });
        });

        return services;
    }

    private static PasskeyRateLimitOptions ResolveRateLimits(HttpContext context)
        => context.RequestServices.GetRequiredService<IOptions<PasskeyOptions>>().Value.RateLimits;

    private static RateLimitPartition<string> PerIp(
        HttpContext context,
        PasskeyRateLimitRule rule,
        bool enabled)
    {
        var key = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        if (!enabled)
        {
            return RateLimitPartition.GetNoLimiter(key);
        }

        return RateLimitPartition.GetFixedWindowLimiter(
            key,
            _ => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = rule.PermitLimit,
                QueueLimit = 0,
                Window = TimeSpan.FromSeconds(rule.WindowSeconds)
            });
    }

    private static RateLimitPartition<string> PerActorOrIp(
        HttpContext context,
        PasskeyRateLimitRule rule,
        bool enabled)
    {
        var actorId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        var key = string.IsNullOrWhiteSpace(actorId)
            ? $"ip:{context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}"
            : $"actor:{actorId}";
        if (!enabled)
        {
            return RateLimitPartition.GetNoLimiter(key);
        }

        return RateLimitPartition.GetFixedWindowLimiter(
            key,
            _ => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = rule.PermitLimit,
                QueueLimit = 0,
                Window = TimeSpan.FromSeconds(rule.WindowSeconds)
            });
    }
}
