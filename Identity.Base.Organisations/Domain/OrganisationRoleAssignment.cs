using System;

namespace Identity.Base.Organisations.Domain;

public class OrganisationRoleAssignment
{
    public Guid OrganisationId { get; set; }

    public Guid UserId { get; set; }

    public Guid RoleId { get; set; }

    public Guid? TenantId { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public Organisation? Organisation { get; set; }

    public OrganisationRole? Role { get; set; }

    public OrganisationMembership? Membership { get; set; }
}
