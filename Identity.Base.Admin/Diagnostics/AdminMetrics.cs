using System.Diagnostics;
using System.Diagnostics.Metrics;
using Identity.Base.Admin.Features.AdminRoles;
using Identity.Base.Admin.Features.AdminUsers;
using AdminUserListQuery = Identity.Base.Admin.Features.AdminUsers.AdminUserListQuery;
using AdminRoleListQuery = Identity.Base.Admin.Features.AdminRoles.AdminRoleEndpoints.AdminRoleListQuery;

namespace Identity.Base.Admin.Diagnostics;

internal static class AdminMetrics
{
    private const string MeterName = "Identity.Base.Admin";
    private static readonly Meter Meter = new(MeterName, "1.0.0");

    internal static readonly Histogram<double> UsersListDuration = Meter.CreateHistogram<double>(
        "identity_admin_users_list_duration_ms",
        unit: "ms",
        description: "Duration of admin user list requests.");

    internal static readonly Histogram<int> UsersListResultCount = Meter.CreateHistogram<int>(
        "identity_admin_users_list_result_count",
        unit: "items",
        description: "Number of users returned by admin user list requests.");

    internal static readonly Histogram<double> RolesListDuration = Meter.CreateHistogram<double>(
        "identity_admin_roles_list_duration_ms",
        unit: "ms",
        description: "Duration of admin role list requests.");

    internal static readonly Histogram<int> RolesListResultCount = Meter.CreateHistogram<int>(
        "identity_admin_roles_list_result_count",
        unit: "items",
        description: "Number of roles returned by admin role list requests.");

    internal static readonly Histogram<double> PermissionsListDuration = Meter.CreateHistogram<double>(
        "identity_admin_permissions_list_duration_ms",
        unit: "ms",
        description: "Duration of admin permission list requests.");

    internal static readonly Histogram<int> PermissionsListResultCount = Meter.CreateHistogram<int>(
        "identity_admin_permissions_list_result_count",
        unit: "items",
        description: "Number of permissions returned by admin permission list requests.");

    internal static TagList BuildUserQueryTags(AdminUserListQuery query)
    {
        var tags = new TagList
        {
            { "sort", string.IsNullOrWhiteSpace(query.Sort) ? "createdAt:desc" : query.Sort },
            { "filter.role", string.IsNullOrWhiteSpace(query.Role) ? "false" : "true" },
            { "filter.search", string.IsNullOrWhiteSpace(query.Search) ? "false" : "true" },
            { "filter.locked", query.Locked.HasValue ? (query.Locked.Value ? "locked" : "unlocked") : "all" },
            { "filter.deleted", query.Deleted.HasValue ? (query.Deleted.Value ? "deleted" : "active") : "all" }
        };

        return tags;
    }

    internal static TagList BuildRoleQueryTags(AdminRoleListQuery query)
    {
        var tags = new TagList
        {
            { "sort", string.IsNullOrWhiteSpace(query.Sort) ? "name" : query.Sort },
            { "filter.search", string.IsNullOrWhiteSpace(query.Search) ? "false" : "true" },
            { "filter.system", query.IsSystemRole.HasValue ? (query.IsSystemRole.Value ? "system" : "custom") : "all" }
        };

        return tags;
    }

    internal static TagList BuildPermissionQueryTags(AdminPermissionListQuery query)
    {
        var tags = new TagList
        {
            { "sort", string.IsNullOrWhiteSpace(query.Sort) ? "name" : query.Sort },
            { "filter.search", string.IsNullOrWhiteSpace(query.Search) ? "false" : "true" }
        };

        return tags;
    }
}
