namespace Identity.Base.Options;

public sealed class ExternalAuthenticationOptions
{
    public const string SectionName = "Authentication:External";

    /// <summary>
    /// When true, an external login callback may automatically associate with an existing
    /// user that has the same email address but no existing external login link.
    /// </summary>
    public bool AutoLinkByEmailOnLogin { get; set; } = true;

    /// <summary>
    /// When true, automatic email-based linking requires an explicit verified-email claim
    /// from the external provider (for example: "email_verified": "true").
    /// </summary>
    public bool RequireVerifiedEmailForAutoLinkByEmail { get; set; }

    /// <summary>
    /// External provider claim types that should be synchronized to the local user claim store
    /// after a successful external login or link. Configured claim types are updated from the
    /// external principal, and stale local values for those types are removed. Leave empty to
    /// avoid persisting provider metadata.
    /// </summary>
    public IList<string> PersistedClaimTypes { get; init; } = new List<string>();
}
