using System;
using System.Collections.Generic;

namespace Identity.Base.Organisations.Domain;

public class OrganisationMembership
{
    public Guid OrganisationId { get; set; }

    public Guid UserId { get; set; }

    public Guid? TenantId { get; set; }

    public bool IsPrimary { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public Organisation? Organisation { get; set; }

    public ICollection<OrganisationRoleAssignment> RoleAssignments { get; set; } = new List<OrganisationRoleAssignment>();
}
