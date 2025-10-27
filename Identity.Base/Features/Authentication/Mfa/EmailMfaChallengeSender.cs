using System.Collections.Generic;
using Identity.Base.Features.Email;
using Identity.Base.Identity;
using Identity.Base.Options;
using Microsoft.Extensions.Options;

namespace Identity.Base.Features.Authentication.Mfa;

internal sealed class EmailMfaChallengeSender : IMfaChallengeSender
{
    private readonly ITemplatedEmailSender _emailSender;
    private readonly IOptions<MfaOptions> _mfaOptions;

    public EmailMfaChallengeSender(
        ITemplatedEmailSender emailSender,
        IOptions<MfaOptions> mfaOptions)
    {
        _emailSender = emailSender;
        _mfaOptions = mfaOptions;
    }

    public string Method => "email";

    public async Task SendChallengeAsync(ApplicationUser user, string code, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(user.Email) || !user.EmailConfirmed)
        {
            throw new InvalidOperationException("User does not have a confirmed email address.");
        }

        if (!_mfaOptions.Value.Email.Enabled)
        {
            throw new InvalidOperationException("Email MFA challenge is disabled.");
        }

        var variables = new Dictionary<string, object?>
        {
            ["email"] = user.Email,
            ["displayName"] = user.DisplayName ?? user.Email,
            ["code"] = code
        };

        var email = new TemplatedEmail(
            TemplatedEmailKeys.EmailMfaChallenge,
            user.Email!,
            user.DisplayName ?? user.Email!,
            variables,
            "Your verification code");

        await _emailSender.SendAsync(email, cancellationToken);
    }
}
