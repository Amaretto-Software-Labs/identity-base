namespace Identity.Base.Roles.Options;

public sealed class RoleConfigurationOptions
{
    public const string SectionName = "Roles";

    public IList<RoleDefinition> Definitions { get; set; } = new List<RoleDefinition>();
    public IList<string> DefaultUserRoles { get; set; } = new List<string>();
    public IList<string> DefaultAdminRoles { get; set; } = new List<string>();
}

public sealed class RoleDefinition
{
    public required string Name { get; set; }
    public string? Description { get; set; }
    public IList<string> Permissions { get; set; } = new List<string>();
    public bool IsSystemRole { get; set; }
}
