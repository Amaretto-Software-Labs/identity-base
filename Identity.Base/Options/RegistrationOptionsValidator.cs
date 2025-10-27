using Microsoft.Extensions.Options;
using System;
using System.Text.RegularExpressions;

namespace Identity.Base.Options;

internal sealed class RegistrationOptionsValidator : IValidateOptions<RegistrationOptions>
{
    public ValidateOptionsResult Validate(string? name, RegistrationOptions options)
    {
        if (options.ProfileFields is null)
        {
            return ValidateOptionsResult.Fail("Registration:ProfileFields must be configured (use an empty array if no additional metadata is required).");
        }

        var nameSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in options.ProfileFields)
        {
            if (!nameSet.Add(field.Name))
            {
                return ValidateOptionsResult.Fail($"Registration profile field '{field.Name}' is duplicated.");
            }

            if (field.MaxLength <= 0)
            {
                return ValidateOptionsResult.Fail($"Registration profile field '{field.Name}' must have MaxLength greater than zero.");
            }

            if (!string.IsNullOrWhiteSpace(field.Pattern))
            {
                try
                {
                    _ = Regex.Match(string.Empty, field.Pattern);
                }
                catch (ArgumentException ex)
                {
                    return ValidateOptionsResult.Fail($"Registration profile field '{field.Name}' pattern is invalid: {ex.Message}");
                }
            }
        }

        if (!options.ConfirmationUrlTemplate.Contains("{token}", StringComparison.Ordinal) ||
            !options.ConfirmationUrlTemplate.Contains("{userId}", StringComparison.Ordinal))
        {
            return ValidateOptionsResult.Fail("Registration confirmation URL template must contain {token} and {userId} placeholders.");
        }

        if (!options.PasswordResetUrlTemplate.Contains("{token}", StringComparison.Ordinal) ||
            !options.PasswordResetUrlTemplate.Contains("{email}", StringComparison.Ordinal))
        {
            return ValidateOptionsResult.Fail("Registration password reset URL template must contain {token} and {email} placeholders.");
        }

        return ValidateOptionsResult.Success;
    }
}
