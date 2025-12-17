using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace Identity.Base.Email.SendGrid;

/// <summary>
/// Configuration options for sending templated emails via SendGrid.
/// </summary>
public sealed class SendGridOptions
{
    /// <summary>
    /// Configuration section name used to bind <see cref="SendGridOptions"/>.
    /// </summary>
    public const string SectionName = "SendGrid";

    /// <summary>
    /// When false the SendGrid sender is skipped and no email is dispatched.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// SendGrid API key used for authentication.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

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
    public SendGridTemplateOptions Templates { get; set; } = new();
}

/// <summary>
/// Template identifiers used when dispatching SendGrid dynamic template emails.
/// </summary>
public sealed class SendGridTemplateOptions
{
    /// <summary>
    /// Template identifier for the account confirmation email.
    /// </summary>
    public string Confirmation { get; set; } = string.Empty;

    /// <summary>
    /// Template identifier for the password reset email.
    /// </summary>
    public string PasswordReset { get; set; } = string.Empty;

    /// <summary>
    /// Template identifier for the MFA challenge email.
    /// </summary>
    public string MfaChallenge { get; set; } = string.Empty;
}

internal sealed class SendGridOptionsValidator : IValidateOptions<SendGridOptions>
{
    public ValidateOptionsResult Validate(string? name, SendGridOptions options)
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
            if (string.IsNullOrWhiteSpace(options.Templates.Confirmation))
            {
                missing.Add("Templates.Confirmation");
            }

            if (string.IsNullOrWhiteSpace(options.Templates.PasswordReset))
            {
                missing.Add("Templates.PasswordReset");
            }

            if (string.IsNullOrWhiteSpace(options.Templates.MfaChallenge))
            {
                missing.Add("Templates.MfaChallenge");
            }
        }

        return missing.Count > 0
            ? ValidateOptionsResult.Fail($"SendGrid options missing required values: {string.Join(", ", missing)}")
            : ValidateOptionsResult.Success;
    }
}

