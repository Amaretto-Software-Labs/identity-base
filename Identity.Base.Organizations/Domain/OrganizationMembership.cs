using System;
using System.Collections.Generic;

namespace Identity.Base.Organizations.Domain;

public class OrganizationMembership
{
    public Guid OrganizationId { get; set; }

    public Guid UserId { get; set; }

    public Guid? TenantId { get; set; }

    public bool IsPrimary { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public Organization? Organization { get; set; }

    public ICollection<OrganizationRoleAssignment> RoleAssignments { get; set; } = new List<OrganizationRoleAssignment>();
}
