namespace Identity.Base.Features.Email;

public static class TemplatedEmailKeys
{
    public const string AccountConfirmation = "account.confirmation";
    public const string PasswordReset = "account.passwordReset";
    public const string EmailMfaChallenge = "mfa.email";
    public const string PasskeySignupConfirmation = "passkey.signup.confirmation";
    public const string PasskeyRecoveryConfirmation = "passkey.recovery.confirmation";
    public const string PasskeyRecoveryCompleted = "passkey.recovery.completed";
    public const string PasskeysReset = "passkey.reset";
}
