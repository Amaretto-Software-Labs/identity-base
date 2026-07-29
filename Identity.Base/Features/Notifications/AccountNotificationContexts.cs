using Identity.Base.Features.Email;
using Identity.Base.Identity;

namespace Identity.Base.Features.Notifications;

public sealed class EmailConfirmationNotificationContext : NotificationContext
{
    public EmailConfirmationNotificationContext(ApplicationUser user, string confirmationUrl)
        : base(TemplatedEmailKeys.AccountConfirmation, user)
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
        : base(TemplatedEmailKeys.PasswordReset, user)
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
        : base(TemplatedEmailKeys.EmailMfaChallenge, user)
    {
        Code = code;
        Variables["email"] = user.Email;
        Variables["displayName"] = user.DisplayName ?? user.Email;
        Variables["code"] = code;
    }

    public string Code { get; set; }
}

public sealed class PasskeySignupConfirmationNotificationContext : NotificationContext
{
    public PasskeySignupConfirmationNotificationContext(ApplicationUser user, string confirmationUrl, string mode)
        : base(TemplatedEmailKeys.PasskeySignupConfirmation, user)
    {
        ConfirmationUrl = confirmationUrl;
        Mode = mode;
        Variables["email"] = user.Email;
        Variables["displayName"] = user.DisplayName ?? user.Email;
        Variables["confirmationUrl"] = confirmationUrl;
        Variables["registrationMode"] = mode;
    }

    public string ConfirmationUrl { get; set; }

    public string Mode { get; }
}

public sealed class PasskeyRecoveryConfirmationNotificationContext : NotificationContext
{
    public PasskeyRecoveryConfirmationNotificationContext(ApplicationUser user, string confirmationUrl)
        : base(TemplatedEmailKeys.PasskeyRecoveryConfirmation, user)
    {
        ConfirmationUrl = confirmationUrl;
        Variables["email"] = user.Email;
        Variables["displayName"] = user.DisplayName ?? user.Email;
        Variables["confirmationUrl"] = confirmationUrl;
    }

    public string ConfirmationUrl { get; set; }
}

public sealed class PasskeyRecoveryCompletedNotificationContext : NotificationContext
{
    public PasskeyRecoveryCompletedNotificationContext(ApplicationUser user)
        : base(TemplatedEmailKeys.PasskeyRecoveryCompleted, user)
    {
        Variables["email"] = user.Email;
        Variables["displayName"] = user.DisplayName ?? user.Email;
    }
}

public sealed class PasskeysResetNotificationContext : NotificationContext
{
    public PasskeysResetNotificationContext(ApplicationUser user, int revokedCount)
        : base(TemplatedEmailKeys.PasskeysReset, user)
    {
        Variables["email"] = user.Email;
        Variables["displayName"] = user.DisplayName ?? user.Email;
        Variables["revokedCount"] = revokedCount;
    }
}
