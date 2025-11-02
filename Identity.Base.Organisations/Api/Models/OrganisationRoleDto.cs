namespace Identity.Base.Organisations.Api.Models;

public sealed class OrganisationRoleDto
{
    public Guid Id { get; init; }

    public Guid? OrganisationId { get; init; }

    public Guid? TenantId { get; init; }

    public string Name { get; init; } = string.Empty;

    public string? Description { get; init; }

    public bool IsSystemRole { get; init; }

    public DateTimeOffset CreatedAtUtc { get; init; }

    public DateTimeOffset? UpdatedAtUtc { get; init; }
}
