using System.Threading.RateLimiting;
using Identity.Base.Options;
using Microsoft.Extensions.Options;

namespace Identity.Base.Features.Authentication.Passkeys;

internal sealed class PasskeyDraftRateLimiter(IOptions<PasskeyOptions> options) : IDisposable
{
    private readonly bool _enabled = options.Value.RateLimits.Enabled;
    private readonly PartitionedRateLimiter<Guid> _signupLimiter =
        CreateLimiter(options.Value.RateLimits.SignupEnrollment);
    private readonly PartitionedRateLimiter<Guid> _recoveryLimiter =
        CreateLimiter(options.Value.RateLimits.RecoveryEnrollment);

    public bool TryAcquire(string scope, Guid draftId)
    {
        if (!_enabled)
        {
            return true;
        }

        var limiter = string.Equals(scope, "signup", StringComparison.Ordinal)
            ? _signupLimiter
            : _recoveryLimiter;
        using var lease = limiter.AttemptAcquire(draftId);
        return lease.IsAcquired;
    }

    public void Dispose()
    {
        _signupLimiter.Dispose();
        _recoveryLimiter.Dispose();
    }

    private static PartitionedRateLimiter<Guid> CreateLimiter(PasskeyRateLimitRule rule)
        => PartitionedRateLimiter.Create<Guid, Guid>(
            draftId => RateLimitPartition.GetFixedWindowLimiter(
                draftId,
                _ => new FixedWindowRateLimiterOptions
                {
                    AutoReplenishment = true,
                    PermitLimit = rule.PermitLimit,
                    QueueLimit = 0,
                    Window = TimeSpan.FromSeconds(rule.WindowSeconds)
                }));
}
