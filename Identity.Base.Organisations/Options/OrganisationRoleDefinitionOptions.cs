using System.Collections.Generic;

namespace Identity.Base.Organisations.Options;

public enum OrganisationRoleDefaultType
{
    Owner,
    Manager,
    Member
}

public sealed class OrganisationRoleDefinitionOptions
{
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsSystemRole { get; set; } = true;

    public OrganisationRoleDefaultType? DefaultType { get; set; }

    public List<string> Permissions { get; set; } = new();

    public static OrganisationRoleDefinitionOptions CreateOwner() => new()
    {
        DefaultType = OrganisationRoleDefaultType.Owner,
        Name = "OrgOwner",
        Description = "Organisation owner: full management access.",
        Permissions =
        [
            "organisations.read",
            "organisations.manage",
            "organisation.members.read",
            "organisation.members.manage",
            "organisation.roles.read",
            "organisation.roles.manage"
        ]
    };

    public static OrganisationRoleDefinitionOptions CreateManager() => new()
    {
        DefaultType = OrganisationRoleDefaultType.Manager,
        Name = "OrgManager",
        Description = "Organisation manager: manage members and settings.",
        Permissions =
        [
            "organisations.read",
            "organisation.members.read",
            "organisation.members.manage",
            "organisation.roles.read"
        ]
    };

    public static OrganisationRoleDefinitionOptions CreateMember() => new()
    {
        DefaultType = OrganisationRoleDefaultType.Member,
        Name = "OrgMember",
        Description = "Organisation member: default access level.",
        Permissions =
        [
            "organisations.read"
        ]
    };
}
