namespace Identity.Base.ServicePrincipals.Options;

public sealed class ServicePrincipalOptions
{
    public const string SectionName = "Identity:ServicePrincipals";

    public TimeSpan AccessTokenLifetime { get; set; } = TimeSpan.FromMinutes(15);
    public IList<string> AllowedScopes { get; set; } = new List<string> { "identity.api" };
}

public static class ServicePrincipalPermissions
{
    public const string Read = "service-principals.read";
    public const string Create = "service-principals.create";
    public const string Update = "service-principals.update";
    public const string Disable = "service-principals.disable";
    public const string ManageRoles = "service-principals.manage-roles";
    public const string ManageCredentials = "service-principals.manage-credentials";

    public static IReadOnlyList<string> All { get; } =
        [Read, Create, Update, Disable, ManageRoles, ManageCredentials];
}
