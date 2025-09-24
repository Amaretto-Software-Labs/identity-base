using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace Identity.Base.Options;

public sealed class MfaOptions
{
    public const string SectionName = "Mfa";

    [Required]
    [MaxLength(128)]
    public string Issuer { get; set; } = "Identity Base";

    public EmailChallengeOptions Email { get; init; } = new();

    public SmsChallengeOptions Sms { get; init; } = new();
}

public sealed class EmailChallengeOptions
{
    public bool Enabled { get; set; } = true;
}

public sealed class SmsChallengeOptions
{
    public bool Enabled { get; set; }

    [MaxLength(20)]
    public string FromPhoneNumber { get; set; } = string.Empty;

    [MaxLength(128)]
    public string AccountSid { get; set; } = string.Empty;

    [MaxLength(128)]
    public string AuthToken { get; set; } = string.Empty;
}

public sealed class MfaOptionsValidator : IValidateOptions<MfaOptions>
{
    public ValidateOptionsResult Validate(string? name, MfaOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Issuer))
        {
            return ValidateOptionsResult.Fail("Mfa issuer must be configured.");
        }

        if (options.Sms.Enabled)
        {
            var missing = new List<string>();

            if (string.IsNullOrWhiteSpace(options.Sms.AccountSid))
            {
                missing.Add("Sms.AccountSid");
            }

            if (string.IsNullOrWhiteSpace(options.Sms.AuthToken))
            {
                missing.Add("Sms.AuthToken");
            }

            if (string.IsNullOrWhiteSpace(options.Sms.FromPhoneNumber) || options.Sms.FromPhoneNumber.Length is < 8 or > 20)
            {
                missing.Add("Sms.FromPhoneNumber");
            }

            if (missing.Count > 0)
            {
                return ValidateOptionsResult.Fail("MFA SMS options missing required values: " + string.Join(", ", missing));
            }
        }

        return ValidateOptionsResult.Success;
    }
}
