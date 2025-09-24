using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace Identity.Base.Options;

public sealed class ExternalProviderOptions
{
    public const string SectionName = "ExternalProviders";

    public OAuthProviderOptions Google { get; init; } = new();

    public OAuthProviderOptions Microsoft { get; init; } = new();

    public AppleProviderOptions Apple { get; init; } = new();
}

public class OAuthProviderOptions
{
    public bool Enabled { get; set; }

    [MaxLength(256)]
    public string ClientId { get; set; } = string.Empty;

    [MaxLength(256)]
    public string ClientSecret { get; set; } = string.Empty;

    [MaxLength(256)]
    public string CallbackPath { get; set; } = string.Empty;

    public IList<string> Scopes { get; init; } = new List<string>();
}

public sealed class AppleProviderOptions : OAuthProviderOptions
{
    [MaxLength(256)]
    public string TeamId { get; set; } = string.Empty;

    [MaxLength(256)]
    public string KeyId { get; set; } = string.Empty;

    public string PrivateKey { get; set; } = string.Empty;
}

public sealed class ExternalProviderOptionsValidator : IValidateOptions<ExternalProviderOptions>
{
    public ValidateOptionsResult Validate(string? name, ExternalProviderOptions options)
    {
        var failures = new List<string>();

        ValidateOAuthOptions("Google", options.Google, failures);
        ValidateOAuthOptions("Microsoft", options.Microsoft, failures);
        ValidateAppleOptions(options.Apple, failures);

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(string.Join("; ", failures))
            : ValidateOptionsResult.Success;
    }

    private static void ValidateOAuthOptions(string providerName, OAuthProviderOptions options, List<string> failures)
    {
        if (!options.Enabled)
        {
            return;
        }

        var missing = new List<string>();

        if (string.IsNullOrWhiteSpace(options.ClientId))
        {
            missing.Add("ClientId");
        }

        if (string.IsNullOrWhiteSpace(options.ClientSecret))
        {
            missing.Add("ClientSecret");
        }

        if (string.IsNullOrWhiteSpace(options.CallbackPath))
        {
            missing.Add("CallbackPath");
        }

        if (missing.Count > 0)
        {
            failures.Add($"{providerName} provider missing required values: {string.Join(", ", missing)}");
        }
    }

    private static void ValidateAppleOptions(AppleProviderOptions options, List<string> failures)
    {
        if (!options.Enabled)
        {
            return;
        }

        var missing = new List<string>();

        if (string.IsNullOrWhiteSpace(options.ClientId))
        {
            missing.Add("ClientId");
        }

        var hasClientSecret = !string.IsNullOrWhiteSpace(options.ClientSecret);
        var hasJwtCredentials = !string.IsNullOrWhiteSpace(options.PrivateKey)
            && !string.IsNullOrWhiteSpace(options.TeamId)
            && !string.IsNullOrWhiteSpace(options.KeyId);

        if (!hasClientSecret && !hasJwtCredentials)
        {
            missing.Add("ClientSecret or PrivateKey/TeamId/KeyId");
        }

        if (string.IsNullOrWhiteSpace(options.CallbackPath))
        {
            missing.Add("CallbackPath");
        }

        if (missing.Count > 0)
        {
            failures.Add($"Apple provider missing required values: {string.Join(", ", missing)}");
        }
    }
}
