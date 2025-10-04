using System;

namespace Identity.Base.Organizations.Abstractions;

public sealed class OrganizationRoleCreateRequest
{
    public Guid? OrganizationId { get; init; }

    public Guid? TenantId { get; init; }

    public string Name { get; init; } = string.Empty;

    public string? Description { get; init; }

    public bool IsSystemRole { get; init; }
}
