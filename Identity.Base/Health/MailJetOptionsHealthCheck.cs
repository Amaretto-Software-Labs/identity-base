using Identity.Base.Options;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Identity.Base.Health;

public sealed class MailJetOptionsHealthCheck : IHealthCheck
{
    private readonly IOptions<MailJetOptions> _options;

    public MailJetOptionsHealthCheck(IOptions<MailJetOptions> options)
    {
        _options = options;
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var options = _options.Value;
        if (string.IsNullOrWhiteSpace(options.ApiKey) || string.IsNullOrWhiteSpace(options.ApiSecret) || string.IsNullOrWhiteSpace(options.FromEmail))
        {
            return Task.FromResult(HealthCheckResult.Degraded("MailJet configuration is incomplete."));
        }

        if (options.Templates.MfaChallenge <= 0 || options.Templates.Confirmation <= 0 || options.Templates.PasswordReset <= 0)
        {
            return Task.FromResult(HealthCheckResult.Degraded("MailJet templates missing."));
        }

        return Task.FromResult(HealthCheckResult.Healthy());
    }
}
