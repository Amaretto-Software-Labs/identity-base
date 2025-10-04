namespace Identity.Base.Organizations.Options;

public sealed class OrganizationRoleOptions
{
    public int NameMaxLength { get; set; } = 128;

    public int DescriptionMaxLength { get; set; } = 512;

    public string OwnerRoleName { get; set; } = "OrgOwner";

    public string ManagerRoleName { get; set; } = "OrgManager";

    public string MemberRoleName { get; set; } = "OrgMember";
}
