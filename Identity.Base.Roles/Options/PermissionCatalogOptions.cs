namespace Identity.Base.Roles.Options;

public sealed class PermissionCatalogOptions
{
    public const string SectionName = "Permissions";

    public IList<PermissionDefinition> Definitions { get; set; } = new List<PermissionDefinition>();
}

public sealed class PermissionDefinition
{
    public required string Name { get; set; }
    public string? Description { get; set; }
}
