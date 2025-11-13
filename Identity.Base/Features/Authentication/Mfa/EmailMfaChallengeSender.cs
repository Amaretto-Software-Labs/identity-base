using System.Collections.Generic;
using Identity.Base.Features.Email;
using Identity.Base.Identity;
using Identity.Base.Options;
using Identity.Base.Features.Notifications;
using Microsoft.Extensions.Options;

namespace Identity.Base.Features.Authentication.Mfa;

internal sealed class EmailMfaChallengeSender : IMfaChallengeSender
{
    private readonly ITemplatedEmailSender _emailSender;
    private readonly IOptions<MfaOptions> _mfaOptions;
    private readonly INotificationContextPipeline<EmailMfaChallengeNotificationContext> _pipeline;

    public EmailMfaChallengeSender(
        ITemplatedEmailSender emailSender,
        IOptions<MfaOptions> mfaOptions,
        INotificationContextPipeline<EmailMfaChallengeNotificationContext> pipeline)
    {
        _emailSender = emailSender;
        _mfaOptions = mfaOptions;
        _pipeline = pipeline;
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

        var context = new EmailMfaChallengeNotificationContext(user, code);
        await _pipeline.RunAsync(context, cancellationToken);
        await _emailSender.SendAsync(context.ToTemplatedEmail(), cancellationToken);
    }
}
