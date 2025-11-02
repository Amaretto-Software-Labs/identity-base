using System;

namespace Identity.Base.Organizations.Domain;

public sealed class OrganizationRolePermission
{
    public Guid Id { get; set; }

    public Guid RoleId { get; set; }

    public Guid PermissionId { get; set; }

    public Guid? TenantId { get; set; }

    public Guid? OrganizationId { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public OrganizationRole? Role { get; set; }
}
