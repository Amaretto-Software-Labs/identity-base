using Identity.Base.Features.Email;
using Identity.Base.Features.Notifications;
using Identity.Base.Identity;

namespace Identity.Base.Features.Authentication.Passkeys;

internal sealed class PasskeyEmailService(
    ITemplatedEmailSender emailSender,
    INotificationContextPipeline<PasskeySignupConfirmationNotificationContext> signupPipeline,
    INotificationContextPipeline<PasskeyRecoveryConfirmationNotificationContext> recoveryPipeline,
    INotificationContextPipeline<PasskeyRecoveryCompletedNotificationContext> recoveryCompletedPipeline)
{
    public async Task SendSignupConfirmationAsync(
        ApplicationUser user,
        string confirmationUrl,
        string mode,
        CancellationToken cancellationToken)
    {
        var context = new PasskeySignupConfirmationNotificationContext(user, confirmationUrl, mode);
        await signupPipeline.RunAsync(context, cancellationToken);
        await emailSender.SendAsync(context.ToTemplatedEmail(), cancellationToken);
    }

    public async Task SendRecoveryConfirmationAsync(
        ApplicationUser user,
        string confirmationUrl,
        CancellationToken cancellationToken)
    {
        var context = new PasskeyRecoveryConfirmationNotificationContext(user, confirmationUrl);
        await recoveryPipeline.RunAsync(context, cancellationToken);
        await emailSender.SendAsync(context.ToTemplatedEmail(), cancellationToken);
    }

    public async Task SendRecoveryCompletedAsync(
        ApplicationUser user,
        CancellationToken cancellationToken)
    {
        var context = new PasskeyRecoveryCompletedNotificationContext(user);
        await recoveryCompletedPipeline.RunAsync(context, cancellationToken);
        await emailSender.SendAsync(context.ToTemplatedEmail(), cancellationToken);
    }
}
