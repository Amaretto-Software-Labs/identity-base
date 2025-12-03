using System;
using System.Collections.Generic;

namespace Identity.Base.Organizations.Domain;

public class OrganizationRole
{
    public Guid Id { get; set; }

    public Guid OrganizationId { get; set; }

    public Guid TenantId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsSystemRole { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public Organization? Organization { get; set; }

    public ICollection<OrganizationRoleAssignment> RoleAssignments { get; set; } = new List<OrganizationRoleAssignment>();

    public ICollection<OrganizationRolePermission> RolePermissions { get; set; } = new List<OrganizationRolePermission>();
}
