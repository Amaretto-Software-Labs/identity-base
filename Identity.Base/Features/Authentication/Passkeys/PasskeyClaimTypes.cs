namespace Identity.Base.Features.Authentication.Passkeys;

/// <summary>
/// Claim types emitted for passkey-authenticated application sessions.
/// </summary>
public static class PasskeyClaimTypes
{
    /// <summary>
    /// Marks a session established by the passkey account-recovery flow.
    /// Hosts can require additional verification or a cooling-off period for
    /// sensitive operations when this claim is present.
    /// </summary>
    public const string Recovery = "identity:passkey_recovery";
}
