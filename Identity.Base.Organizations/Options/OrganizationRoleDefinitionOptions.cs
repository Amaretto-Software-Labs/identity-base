using System.Collections.Generic;

namespace Identity.Base.Organizations.Options;

public enum OrganizationRoleDefaultType
{
    Owner,
    Manager,
    Member
}

public sealed class OrganizationRoleDefinitionOptions
{
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsSystemRole { get; set; } = true;

    public OrganizationRoleDefaultType? DefaultType { get; set; }

    public List<string> Permissions { get; set; } = new();

    public static OrganizationRoleDefinitionOptions CreateOwner() => new()
    {
        DefaultType = OrganizationRoleDefaultType.Owner,
        Name = "OrgOwner",
        Description = "Organization owner: full management access.",
        Permissions =
        [
            "organizations.read",
            "organizations.manage",
            "organization.members.read",
            "organization.members.manage",
            "organization.roles.read",
            "organization.roles.manage"
        ]
    };

    public static OrganizationRoleDefinitionOptions CreateManager() => new()
    {
        DefaultType = OrganizationRoleDefaultType.Manager,
        Name = "OrgManager",
        Description = "Organization manager: manage members and settings.",
        Permissions =
        [
            "organizations.read",
            "organization.members.read",
            "organization.members.manage",
            "organization.roles.read"
        ]
    };

    public static OrganizationRoleDefinitionOptions CreateMember() => new()
    {
        DefaultType = OrganizationRoleDefaultType.Member,
        Name = "OrgMember",
        Description = "Organization member: default access level.",
        Permissions =
        [
            "organizations.read"
        ]
    };
}
