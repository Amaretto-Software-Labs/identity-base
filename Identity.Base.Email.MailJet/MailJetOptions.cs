using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace Identity.Base.Email.MailJet;

/// <summary>
/// Configuration options for sending templated emails via Mailjet.
/// </summary>
public sealed class MailJetOptions
{
    /// <summary>
    /// Configuration section name used to bind <see cref="MailJetOptions"/>.
    /// </summary>
    public const string SectionName = "MailJet";

    /// <summary>
    /// When false the Mailjet sender is skipped and no email is dispatched.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// From email address used for all messages.
    /// </summary>
    public string FromEmail { get; set; } = string.Empty;

    /// <summary>
    /// Human-friendly name associated with <see cref="FromEmail"/>.
    /// </summary>
    [MaxLength(128)]
    public string FromName { get; set; } = "Identity Base";

    /// <summary>
    /// Template identifiers for account-related emails.
    /// </summary>
    public MailJetTemplateOptions Templates { get; set; } = new();

    /// <summary>
    /// Mailjet API key used for authentication.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Mailjet API secret used for authentication.
    /// </summary>
    public string ApiSecret { get; set; } = string.Empty;

    /// <summary>
    /// Optional configuration for Mailjet error reporting.
    /// </summary>
    public MailJetErrorReportingOptions ErrorReporting { get; set; } = new();
}

/// <summary>
/// Template identifiers used when dispatching Mailjet transactional emails.
/// </summary>
public sealed class MailJetTemplateOptions
{
    /// <summary>
    /// Template identifier for the account confirmation email.
    /// </summary>
    public long Confirmation { get; set; }

    /// <summary>
    /// Template identifier for the password reset email.
    /// </summary>
    public long PasswordReset { get; set; }

    /// <summary>
    /// Template identifier for the MFA challenge email.
    /// </summary>
    public long MfaChallenge { get; set; }
}

/// <summary>
/// Controls Mailjet's template error reporting behaviour.
/// </summary>
public sealed class MailJetErrorReportingOptions
{
    /// <summary>
    /// Indicates whether Mailjet should forward template errors.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Destination email for error reports when <see cref="Enabled"/> is true.
    /// </summary>
    public string Email { get; set; } = string.Empty;
}

internal sealed class MailJetOptionsValidator : IValidateOptions<MailJetOptions>
{
    public ValidateOptionsResult Validate(string? name, MailJetOptions options)
    {
        if (!options.Enabled)
        {
            return ValidateOptionsResult.Success;
        }

        var missing = new List<string>();

        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            missing.Add(nameof(options.ApiKey));
        }

        if (string.IsNullOrWhiteSpace(options.ApiSecret))
        {
            missing.Add(nameof(options.ApiSecret));
        }

        if (string.IsNullOrWhiteSpace(options.FromEmail))
        {
            missing.Add(nameof(options.FromEmail));
        }

        if (options.Templates is null)
        {
            missing.Add("Templates.Confirmation");
            missing.Add("Templates.PasswordReset");
            missing.Add("Templates.MfaChallenge");
        }
        else
        {
            if (options.Templates.Confirmation <= 0)
            {
                missing.Add("Templates.Confirmation");
            }

            if (options.Templates.PasswordReset <= 0)
            {
                missing.Add("Templates.PasswordReset");
            }

            if (options.Templates.MfaChallenge <= 0)
            {
                missing.Add("Templates.MfaChallenge");
            }
        }

        if (options.ErrorReporting is { Enabled: true } reporting && string.IsNullOrWhiteSpace(reporting.Email))
        {
            missing.Add("ErrorReporting.Email");
        }

        return missing.Count > 0
            ? ValidateOptionsResult.Fail($"MailJet options missing required values: {string.Join(", ", missing)}")
            : ValidateOptionsResult.Success;
    }
}
