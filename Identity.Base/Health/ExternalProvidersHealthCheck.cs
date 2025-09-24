using Identity.Base.Options;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using System.Collections.Generic;

namespace Identity.Base.Health;

public sealed class ExternalProvidersHealthCheck : IHealthCheck
{
    private readonly IOptions<ExternalProviderOptions> _options;

    public ExternalProvidersHealthCheck(IOptions<ExternalProviderOptions> options)
    {
        _options = options;
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var options = _options.Value;
        var issues = new List<string>();

        EvaluateProvider("Google", options.Google, issues);
        EvaluateProvider("Microsoft", options.Microsoft, issues);
        EvaluateApple(options.Apple, issues);

        if (issues.Count > 0)
        {
            return Task.FromResult(HealthCheckResult.Degraded(string.Join("; ", issues)));
        }

        return Task.FromResult(HealthCheckResult.Healthy());
    }

    private static void EvaluateProvider(string name, OAuthProviderOptions provider, List<string> issues)
    {
        if (!provider.Enabled)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(provider.ClientId) || string.IsNullOrWhiteSpace(provider.ClientSecret))
        {
            issues.Add($"{name} provider missing credentials.");
        }
    }

    private static void EvaluateApple(AppleProviderOptions provider, List<string> issues)
    {
        if (!provider.Enabled)
        {
            return;
        }

        var hasSecret = !string.IsNullOrWhiteSpace(provider.ClientSecret);
        var hasKey = !string.IsNullOrWhiteSpace(provider.PrivateKey) && !string.IsNullOrWhiteSpace(provider.TeamId) && !string.IsNullOrWhiteSpace(provider.KeyId);

        if (!hasSecret && !hasKey)
        {
            issues.Add("Apple provider missing credentials.");
        }
    }
}
