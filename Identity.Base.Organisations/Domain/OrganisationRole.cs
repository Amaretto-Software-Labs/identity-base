using System;
using System.Collections.Generic;

namespace Identity.Base.Organisations.Domain;

public class OrganisationRole
{
    public Guid Id { get; set; }

    public Guid? OrganisationId { get; set; }

    public Guid? TenantId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsSystemRole { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public Organisation? Organisation { get; set; }

    public ICollection<OrganisationRoleAssignment> RoleAssignments { get; set; } = new List<OrganisationRoleAssignment>();

    public ICollection<OrganisationRolePermission> RolePermissions { get; set; } = new List<OrganisationRolePermission>();
}
