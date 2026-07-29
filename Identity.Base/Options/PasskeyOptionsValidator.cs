using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Identity.Base.Options;

internal sealed class PasskeyOptionsValidator(
    IConfiguration configuration,
    IHostEnvironment environment) : IValidateOptions<PasskeyOptions>
{
    public ValidateOptionsResult Validate(string? name, PasskeyOptions options)
    {
        if (options.Signup is null)
        {
            return ValidateOptionsResult.Fail("Passkeys:Signup must be configured.");
        }

        if (options.Recovery is null)
        {
            return ValidateOptionsResult.Fail("Passkeys:Recovery must be configured.");
        }

        if (options.AllowedOrigins is null)
        {
            return ValidateOptionsResult.Fail("Passkeys:AllowedOrigins must be configured (use an empty array when disabled).");
        }

        if (options.Signup.EnabledModes is null)
        {
            return ValidateOptionsResult.Fail("Passkeys:Signup:EnabledModes must be configured (use an empty array when disabled).");
        }

        var modes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var mode in options.Signup.EnabledModes)
        {
            if (!PasskeySignupModes.All.Contains(mode))
            {
                return ValidateOptionsResult.Fail(
                    $"Passkeys:Signup:EnabledModes contains unsupported mode '{mode}'.");
            }

            if (!modes.Add(mode))
            {
                return ValidateOptionsResult.Fail(
                    $"Passkeys:Signup:EnabledModes contains duplicate mode '{mode}'.");
            }
        }

        if (!options.Enabled)
        {
            return modes.Count == 0
                ? ValidateOptionsResult.Success
                : ValidateOptionsResult.Fail(
                    "Passkeys:Signup:EnabledModes must be empty when Passkeys:Enabled is false.");
        }

        if (!IsValidServerDomain(options.ServerDomain))
        {
            return ValidateOptionsResult.Fail(
                "Passkeys:ServerDomain must be a domain without scheme, port, path, wildcard, or trailing dot.");
        }

        if (options.AllowedOrigins.Count == 0)
        {
            return ValidateOptionsResult.Fail("Passkeys:AllowedOrigins must contain at least one exact origin when enabled.");
        }

        var corsOrigins = configuration
            .GetSection($"{CorsSettings.SectionName}:AllowedOrigins")
            .Get<string[]>() ?? [];
        var corsSet = new HashSet<string>(corsOrigins.Select(NormalizeOrigin), StringComparer.Ordinal);
        var seenOrigins = new HashSet<string>(StringComparer.Ordinal);

        foreach (var configuredOrigin in options.AllowedOrigins)
        {
            if (!Uri.TryCreate(configuredOrigin, UriKind.Absolute, out var uri) ||
                !string.IsNullOrEmpty(uri.UserInfo) ||
                uri.AbsolutePath != "/" ||
                !string.IsNullOrEmpty(uri.Query) ||
                !string.IsNullOrEmpty(uri.Fragment))
            {
                return ValidateOptionsResult.Fail(
                    $"Passkeys:AllowedOrigins entry '{configuredOrigin}' must be an exact absolute origin.");
            }

            var isLocalDevelopment =
                environment.IsDevelopment() &&
                string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase);
            if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) &&
                !isLocalDevelopment)
            {
                return ValidateOptionsResult.Fail(
                    $"Passkeys:AllowedOrigins entry '{configuredOrigin}' must use HTTPS outside localhost development.");
            }

            if (!HostMatchesRelyingParty(uri.Host, options.ServerDomain))
            {
                return ValidateOptionsResult.Fail(
                    $"Passkeys:AllowedOrigins entry '{configuredOrigin}' is outside relying-party domain '{options.ServerDomain}'.");
            }

            var normalized = NormalizeOrigin(configuredOrigin);
            if (!seenOrigins.Add(normalized))
            {
                return ValidateOptionsResult.Fail(
                    $"Passkeys:AllowedOrigins contains duplicate origin '{configuredOrigin}'.");
            }

            if (!corsSet.Contains(normalized))
            {
                return ValidateOptionsResult.Fail(
                    $"Passkeys:AllowedOrigins entry '{configuredOrigin}' must also appear in Cors:AllowedOrigins.");
            }
        }

        if (!string.Equals(options.UserVerification, "required", StringComparison.Ordinal) ||
            !string.Equals(options.ResidentKey, "required", StringComparison.Ordinal) ||
            !string.Equals(options.Attestation, "none", StringComparison.Ordinal))
        {
            return ValidateOptionsResult.Fail(
                "Passkeys require UserVerification='required', ResidentKey='required', and Attestation='none'.");
        }

        if (modes.Count > 0 &&
            !HasRequiredPlaceholders(options.Signup.ConfirmationUrlTemplate))
        {
            return ValidateOptionsResult.Fail(
                "Passkeys:Signup:ConfirmationUrlTemplate must contain {draftId} and {token} placeholders.");
        }

        if (!HasRequiredPlaceholders(options.Recovery.ConfirmationUrlTemplate))
        {
            return ValidateOptionsResult.Fail(
                "Passkeys:Recovery:ConfirmationUrlTemplate must contain {draftId} and {token} placeholders.");
        }

        return ValidateOptionsResult.Success;
    }

    private static bool IsValidServerDomain(string domain)
    {
        if (string.IsNullOrWhiteSpace(domain) ||
            domain.StartsWith(".", StringComparison.Ordinal) ||
            domain.EndsWith(".", StringComparison.Ordinal) ||
            domain.Contains('*') ||
            domain.Contains('/') ||
            domain.Contains(':') ||
            Uri.CheckHostName(domain) is UriHostNameType.Unknown)
        {
            return false;
        }

        return domain.Contains('.', StringComparison.Ordinal) ||
               string.Equals(domain, "localhost", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HostMatchesRelyingParty(string host, string relyingParty)
        => string.Equals(host, relyingParty, StringComparison.OrdinalIgnoreCase) ||
           host.EndsWith($".{relyingParty}", StringComparison.OrdinalIgnoreCase);

    private static bool HasRequiredPlaceholders(string template)
        => !string.IsNullOrWhiteSpace(template) &&
           template.Contains("{draftId}", StringComparison.Ordinal) &&
           template.Contains("{token}", StringComparison.Ordinal) &&
           Uri.TryCreate(
               template
                   .Replace("{draftId}", Guid.Empty.ToString("N"), StringComparison.Ordinal)
                   .Replace("{token}", "token", StringComparison.Ordinal),
               UriKind.Absolute,
               out var uri) &&
           (uri.Scheme == Uri.UriSchemeHttps ||
            (uri.Scheme == Uri.UriSchemeHttp &&
             string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase)));

    internal static string NormalizeOrigin(string origin)
    {
        if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
        {
            return origin.TrimEnd('/');
        }

        var defaultPort =
            (uri.Scheme == Uri.UriSchemeHttps && uri.Port == 443) ||
            (uri.Scheme == Uri.UriSchemeHttp && uri.Port == 80);
        return defaultPort
            ? $"{uri.Scheme.ToLowerInvariant()}://{uri.Host.ToLowerInvariant()}"
            : $"{uri.Scheme.ToLowerInvariant()}://{uri.Host.ToLowerInvariant()}:{uri.Port}";
    }
}
