using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace Identity.Base.Options;

public sealed class MailJetOptions
{
    public const string SectionName = "MailJet";

    [EmailAddress]
    public string FromEmail { get; set; } = string.Empty;

    [MaxLength(128)]
    public string FromName { get; set; } = "Identity Base";

    public MailJetTemplateOptions Templates { get; set; } = new();

    public string ApiKey { get; set; } = string.Empty;

    public string ApiSecret { get; set; } = string.Empty;

    public MailJetErrorReportingOptions ErrorReporting { get; set; } = new();
}

public sealed class MailJetTemplateOptions
{
    public long Confirmation { get; set; }

    public long PasswordReset { get; set; }

    public long MfaChallenge { get; set; }
}

public sealed class MailJetErrorReportingOptions
{
    public bool Enabled { get; set; }

    public string Email { get; set; } = string.Empty;
}

internal sealed class MailJetOptionsValidator : IValidateOptions<MailJetOptions>
{
    public ValidateOptionsResult Validate(string? name, MailJetOptions options)
    {
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
