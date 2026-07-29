using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;
using Identity.Base.Options;
using Microsoft.Extensions.Options;

namespace Identity.Base.Features.Authentication.Passkeys;

internal sealed class PasskeyEmailRateLimiter(IOptions<PasskeyOptions> options) : IDisposable
{
    private readonly byte[] _key = RandomNumberGenerator.GetBytes(32);
    private readonly bool _enabled = options.Value.RateLimits.Enabled;
    private readonly PartitionedRateLimiter<string> _signupLimiter =
        CreateLimiter(options.Value.RateLimits.SignupEmailAddress);
    private readonly PartitionedRateLimiter<string> _recoveryLimiter =
        CreateLimiter(options.Value.RateLimits.RecoveryEmailAddress);

    public bool TryAcquire(string scope, string normalizedEmail)
    {
        if (!_enabled)
        {
            return true;
        }

        var emailHash = HMACSHA256.HashData(
            _key,
            Encoding.UTF8.GetBytes(normalizedEmail));
        var partitionKey = Convert.ToHexString(emailHash);
        var limiter = string.Equals(scope, "signup", StringComparison.Ordinal)
            ? _signupLimiter
            : _recoveryLimiter;
        using var lease = limiter.AttemptAcquire(partitionKey);
        return lease.IsAcquired;
    }

    public void Dispose()
    {
        _signupLimiter.Dispose();
        _recoveryLimiter.Dispose();
        CryptographicOperations.ZeroMemory(_key);
    }

    private static PartitionedRateLimiter<string> CreateLimiter(PasskeyRateLimitRule rule)
        => PartitionedRateLimiter.Create<string, string>(
            partitionKey => RateLimitPartition.GetFixedWindowLimiter(
                partitionKey,
                _ => new FixedWindowRateLimiterOptions
                {
                    AutoReplenishment = true,
                    PermitLimit = rule.PermitLimit,
                    QueueLimit = 0,
                    Window = TimeSpan.FromSeconds(rule.WindowSeconds)
                }));
}
