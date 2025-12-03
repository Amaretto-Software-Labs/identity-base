using System;

namespace Identity.Base.Organizations.Domain;

public class OrganizationRoleAssignment
{
    public Guid OrganizationId { get; set; }

    public Guid UserId { get; set; }

    public Guid RoleId { get; set; }

    public Guid TenantId { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public Organization? Organization { get; set; }

    public OrganizationRole? Role { get; set; }

    public OrganizationMembership? Membership { get; set; }
}
