namespace Identity.Base.Organizations.Api.Models;

public sealed class OrganizationRoleDto
{
    public Guid Id { get; init; }

    public Guid? OrganizationId { get; init; }

    public Guid? TenantId { get; init; }

    public string Name { get; init; } = string.Empty;

    public string? Description { get; init; }

    public bool IsSystemRole { get; init; }

    public DateTimeOffset CreatedAtUtc { get; init; }

    public DateTimeOffset? UpdatedAtUtc { get; init; }
}
