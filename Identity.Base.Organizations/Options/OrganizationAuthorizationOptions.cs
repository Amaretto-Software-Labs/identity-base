namespace Identity.Base.Organizations.Options;

public sealed class OrganizationAuthorizationOptions
{
    /// <summary>
    /// OAuth scope required for admin organization endpoints. Set to <c>null</c> to disable scope enforcement.
    /// </summary>
    public string? AdminRequiredScope { get; set; } = "identity.admin";
}
