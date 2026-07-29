using System.ComponentModel.DataAnnotations;

namespace Identity.Base.Options;

public sealed class PasskeyOptions
{
    public const string SectionName = "Passkeys";

    public bool Enabled { get; set; }

    [MaxLength(253)]
    public string ServerDomain { get; set; } = string.Empty;

    public IList<string> AllowedOrigins { get; set; } = new List<string>();

    [Range(60, 600)]
    public int AuthenticatorTimeoutSeconds { get; set; } = 180;

    [Range(32, 64)]
    public int ChallengeSize { get; set; } = 32;

    public string UserVerification { get; set; } = "required";

    public string ResidentKey { get; set; } = "required";

    public string Attestation { get; set; } = "none";

    [Range(1, 20)]
    public int MaxPasskeysPerUser { get; set; } = 10;

    [Range(1, 200)]
    public int NameMaxLength { get; set; } = 100;

    public PasskeySignupOptions Signup { get; set; } = new();

    public PasskeyRecoveryOptions Recovery { get; set; } = new();

    public PasskeyRateLimitOptions RateLimits { get; set; } = new();
}

public sealed class PasskeyRecoveryOptions
{
    [MaxLength(1024)]
    public string ConfirmationUrlTemplate { get; set; } = string.Empty;

    [Range(10, 60)]
    public int DraftLifetimeMinutes { get; set; } = 30;
}

public sealed class PasskeySignupOptions
{
    public IList<string> EnabledModes { get; set; } = new List<string>();

    [MaxLength(1024)]
    public string ConfirmationUrlTemplate { get; set; } = string.Empty;

    [Range(10, 60)]
    public int DraftLifetimeMinutes { get; set; } = 30;
}

public sealed class PasskeyRateLimitOptions
{
    public bool Enabled { get; set; } = true;

    public PasskeyRateLimitRule Configuration { get; set; } = new(60, 60);

    public PasskeyRateLimitRule AuthenticationOptions { get; set; } = new(20, 60);

    public PasskeyRateLimitRule Authentication { get; set; } = new(10, 60);

    public PasskeyRateLimitRule SignupEmail { get; set; } = new(5, 15 * 60);

    public PasskeyRateLimitRule SignupEnrollment { get; set; } = new(10, 15 * 60);

    public PasskeyRateLimitRule RecoveryEmail { get; set; } = new(3, 60 * 60);

    public PasskeyRateLimitRule RecoveryEnrollment { get; set; } = new(5, 60 * 60);

    public PasskeyRateLimitRule Creation { get; set; } = new(5, 10 * 60);

    public PasskeyRateLimitRule Management { get; set; } = new(20, 10 * 60);

    public PasskeyRateLimitRule Admin { get; set; } = new(10, 60);

    public PasskeyRateLimitRule SignupEmailAddress { get; set; } = new(3, 60 * 60);

    public PasskeyRateLimitRule RecoveryEmailAddress { get; set; } = new(3, 60 * 60);
}

public sealed class PasskeyRateLimitRule
{
    public PasskeyRateLimitRule()
    {
    }

    public PasskeyRateLimitRule(int permitLimit, int windowSeconds)
    {
        PermitLimit = permitLimit;
        WindowSeconds = windowSeconds;
    }

    [Range(1, 10_000)]
    public int PermitLimit { get; set; }

    [Range(1, 86_400)]
    public int WindowSeconds { get; set; }
}

public static class PasskeySignupModes
{
    public const string Assisted = "passkey-assisted";
    public const string Passwordless = "passwordless";

    public static readonly IReadOnlySet<string> All =
        new HashSet<string>([Assisted, Passwordless], StringComparer.Ordinal);
}
