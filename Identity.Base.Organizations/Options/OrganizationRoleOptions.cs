using System;
using System.Collections.Generic;
using System.Linq;

namespace Identity.Base.Organizations.Options;

public sealed class OrganizationRoleOptions
{
    private string? _ownerRoleName;
    private string? _managerRoleName;
    private string? _memberRoleName;

    public int NameMaxLength { get; set; } = 128;

    public int DescriptionMaxLength { get; set; } = 512;

    public List<OrganizationRoleDefinitionOptions> DefaultRoles { get; set; } =
    [
        OrganizationRoleDefinitionOptions.CreateOwner(),
        OrganizationRoleDefinitionOptions.CreateManager(),
        OrganizationRoleDefinitionOptions.CreateMember()
    ];

    public string OwnerRoleName
    {
        get => _ownerRoleName ?? ResolveDefaultName(OrganizationRoleDefaultType.Owner) ?? "OrgOwner";
        set => _ownerRoleName = NormalizeName(value, "OrgOwner");
    }

    public string ManagerRoleName
    {
        get => _managerRoleName ?? ResolveDefaultName(OrganizationRoleDefaultType.Manager) ?? "OrgManager";
        set => _managerRoleName = NormalizeName(value, "OrgManager");
    }

    public string MemberRoleName
    {
        get => _memberRoleName ?? ResolveDefaultName(OrganizationRoleDefaultType.Member) ?? "OrgMember";
        set => _memberRoleName = NormalizeName(value, "OrgMember");
    }

    private string? ResolveDefaultName(OrganizationRoleDefaultType type) =>
        DefaultRoles.FirstOrDefault(role => role.DefaultType == type)?.Name;

    private static string NormalizeName(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        return value.Trim();
    }
}
