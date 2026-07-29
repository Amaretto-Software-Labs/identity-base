using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;

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
            options.AddPolicy(Configuration, context => PerIp(context, 60, TimeSpan.FromMinutes(1)));
            options.AddPolicy(AuthenticationOptions, context => PerIp(context, 20, TimeSpan.FromMinutes(1)));
            options.AddPolicy(Authentication, context => PerIp(context, 10, TimeSpan.FromMinutes(1)));
            options.AddPolicy(SignupEnrollment, context => PerIp(context, 10, TimeSpan.FromMinutes(15)));
            options.AddPolicy(SignupEmail, context => PerIp(context, 5, TimeSpan.FromMinutes(15)));
            options.AddPolicy(RecoveryEnrollment, context => PerIp(context, 5, TimeSpan.FromHours(1)));
            options.AddPolicy(RecoveryEmail, context => PerIp(context, 3, TimeSpan.FromHours(1)));
            options.AddPolicy(Creation, context => PerActorOrIp(context, 5, TimeSpan.FromMinutes(10)));
            options.AddPolicy(Management, context => PerActorOrIp(context, 20, TimeSpan.FromMinutes(10)));
            options.AddPolicy(Admin, context => PerActorOrIp(context, 10, TimeSpan.FromMinutes(1)));
        });

        return services;
    }

    private static RateLimitPartition<string> PerIp(
        HttpContext context,
        int permitLimit,
        TimeSpan window)
    {
        var key = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(
            key,
            _ => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = permitLimit,
                QueueLimit = 0,
                Window = window
            });
    }

    private static RateLimitPartition<string> PerActorOrIp(
        HttpContext context,
        int permitLimit,
        TimeSpan window)
    {
        var actorId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        var key = string.IsNullOrWhiteSpace(actorId)
            ? $"ip:{context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}"
            : $"actor:{actorId}";
        return RateLimitPartition.GetFixedWindowLimiter(
            key,
            _ => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = permitLimit,
                QueueLimit = 0,
                Window = window
            });
    }
}
