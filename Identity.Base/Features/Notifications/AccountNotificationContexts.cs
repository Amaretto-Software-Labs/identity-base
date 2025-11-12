using Identity.Base.Features.Email;
using Identity.Base.Identity;

namespace Identity.Base.Features.Notifications;

public sealed class EmailConfirmationNotificationContext : NotificationContext
{
    public EmailConfirmationNotificationContext(ApplicationUser user, string confirmationUrl)
        : base(TemplatedEmailKeys.AccountConfirmation, user, "Confirm your Identity Base account")
    {
        ConfirmationUrl = confirmationUrl;
        Variables["email"] = user.Email;
        Variables["displayName"] = user.DisplayName ?? user.Email;
        Variables["confirmationUrl"] = confirmationUrl;
    }

    public string ConfirmationUrl { get; set; }
}

public sealed class PasswordResetNotificationContext : NotificationContext
{
    public PasswordResetNotificationContext(ApplicationUser user, string resetUrl)
        : base(TemplatedEmailKeys.PasswordReset, user, "Reset your Identity Base password")
    {
        ResetUrl = resetUrl;
        Variables["email"] = user.Email;
        Variables["displayName"] = user.DisplayName ?? user.Email;
        Variables["resetUrl"] = resetUrl;
    }

    public string ResetUrl { get; set; }
}

public sealed class EmailMfaChallengeNotificationContext : NotificationContext
{
    public EmailMfaChallengeNotificationContext(ApplicationUser user, string code)
        : base(TemplatedEmailKeys.EmailMfaChallenge, user, "Your verification code")
    {
        Code = code;
        Variables["email"] = user.Email;
        Variables["displayName"] = user.DisplayName ?? user.Email;
        Variables["code"] = code;
    }

    public string Code { get; set; }
}
