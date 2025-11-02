using System;

namespace Identity.Base.Organisations.Abstractions;

public sealed class OrganisationRoleCreateRequest
{
    public Guid? OrganisationId { get; init; }

    public Guid? TenantId { get; init; }

    public string Name { get; init; } = string.Empty;

    public string? Description { get; init; }

    public bool IsSystemRole { get; init; }
}
