using System;

namespace Identity.Base.Organisations.Domain;

public sealed class OrganisationRolePermission
{
    public Guid Id { get; set; }

    public Guid RoleId { get; set; }

    public Guid PermissionId { get; set; }

    public Guid? TenantId { get; set; }

    public Guid? OrganisationId { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public OrganisationRole? Role { get; set; }
}
