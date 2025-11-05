namespace Identity.Base.Organizations.Authorization;

public static class UserOrganizationPermissions
{
    public const string OrganizationsRead = "user.organizations.read";
    public const string OrganizationsManage = "user.organizations.manage";
    public const string OrganizationMembersRead = "user.organizations.members.read";
    public const string OrganizationMembersManage = "user.organizations.members.manage";
    public const string OrganizationRolesRead = "user.organizations.roles.read";
    public const string OrganizationRolesManage = "user.organizations.roles.manage";
}

public static class AdminOrganizationPermissions
{
    public const string OrganizationsRead = "admin.organizations.read";
    public const string OrganizationsManage = "admin.organizations.manage";
    public const string OrganizationMembersRead = "admin.organizations.members.read";
    public const string OrganizationMembersManage = "admin.organizations.members.manage";
    public const string OrganizationRolesRead = "admin.organizations.roles.read";
    public const string OrganizationRolesManage = "admin.organizations.roles.manage";
}
