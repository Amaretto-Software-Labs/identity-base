using System;
using System.Collections.Generic;
using System.Linq;

namespace Identity.Base.Organisations.Options;

public sealed class OrganisationRoleOptions
{
    private string? _ownerRoleName;
    private string? _managerRoleName;
    private string? _memberRoleName;

    public int NameMaxLength { get; set; } = 128;

    public int DescriptionMaxLength { get; set; } = 512;

    public List<OrganisationRoleDefinitionOptions> DefaultRoles { get; set; } =
    [
        OrganisationRoleDefinitionOptions.CreateOwner(),
        OrganisationRoleDefinitionOptions.CreateManager(),
        OrganisationRoleDefinitionOptions.CreateMember()
    ];

    public string OwnerRoleName
    {
        get => _ownerRoleName ?? ResolveDefaultName(OrganisationRoleDefaultType.Owner) ?? "OrgOwner";
        set => _ownerRoleName = NormalizeName(value, "OrgOwner");
    }

    public string ManagerRoleName
    {
        get => _managerRoleName ?? ResolveDefaultName(OrganisationRoleDefaultType.Manager) ?? "OrgManager";
        set => _managerRoleName = NormalizeName(value, "OrgManager");
    }

    public string MemberRoleName
    {
        get => _memberRoleName ?? ResolveDefaultName(OrganisationRoleDefaultType.Member) ?? "OrgMember";
        set => _memberRoleName = NormalizeName(value, "OrgMember");
    }

    private string? ResolveDefaultName(OrganisationRoleDefaultType type) =>
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
