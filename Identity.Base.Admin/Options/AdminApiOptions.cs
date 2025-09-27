namespace Identity.Base.Admin.Options;

public sealed class AdminApiOptions
{
    public const string SectionName = "IdentityAdmin";

    /// <summary>
    /// OAuth scope required for admin API access (e.g., "identity.admin"). Leave null to skip scope validation.
    /// </summary>
    public string? RequiredScope { get; set; } = "identity.admin";
}
