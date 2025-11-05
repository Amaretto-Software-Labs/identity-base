using System.Collections.Generic;
using Identity.Base.Organizations.Authorization;

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
            UserOrganizationPermissions.OrganizationsRead,
            UserOrganizationPermissions.OrganizationsManage,
            UserOrganizationPermissions.OrganizationMembersRead,
            UserOrganizationPermissions.OrganizationMembersManage,
            UserOrganizationPermissions.OrganizationRolesRead,
            UserOrganizationPermissions.OrganizationRolesManage
        ]
    };

    public static OrganizationRoleDefinitionOptions CreateManager() => new()
    {
        DefaultType = OrganizationRoleDefaultType.Manager,
        Name = "OrgManager",
        Description = "Organization manager: manage members and settings.",
        Permissions =
        [
            UserOrganizationPermissions.OrganizationsRead,
            UserOrganizationPermissions.OrganizationMembersRead,
            UserOrganizationPermissions.OrganizationMembersManage,
            UserOrganizationPermissions.OrganizationRolesRead
        ]
    };

    public static OrganizationRoleDefinitionOptions CreateMember() => new()
    {
        DefaultType = OrganizationRoleDefaultType.Member,
        Name = "OrgMember",
        Description = "Organization member: default access level.",
        Permissions =
        [
            UserOrganizationPermissions.OrganizationsRead
        ]
    };
}
