using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Identity.Base.Email.SendGrid;

internal sealed class SendGridOptionsHealthCheck : IHealthCheck
{
    private readonly IOptions<SendGridOptions> _options;

    public SendGridOptionsHealthCheck(IOptions<SendGridOptions> options)
    {
        _options = options;
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var options = _options.Value;
        if (!options.Enabled)
        {
            return Task.FromResult(HealthCheckResult.Healthy("SendGrid disabled."));
        }

        if (string.IsNullOrWhiteSpace(options.ApiKey) || string.IsNullOrWhiteSpace(options.FromEmail))
        {
            return Task.FromResult(HealthCheckResult.Degraded("SendGrid configuration is incomplete."));
        }

        if (string.IsNullOrWhiteSpace(options.Templates.MfaChallenge)
            || string.IsNullOrWhiteSpace(options.Templates.Confirmation)
            || string.IsNullOrWhiteSpace(options.Templates.PasswordReset))
        {
            return Task.FromResult(HealthCheckResult.Degraded("SendGrid templates missing."));
        }

        return Task.FromResult(HealthCheckResult.Healthy());
    }
}

