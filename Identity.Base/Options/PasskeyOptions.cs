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

public static class PasskeySignupModes
{
    public const string Assisted = "passkey-assisted";
    public const string Passwordless = "passwordless";

    public static readonly IReadOnlySet<string> All =
        new HashSet<string>([Assisted, Passwordless], StringComparer.Ordinal);
}
