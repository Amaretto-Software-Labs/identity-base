namespace Identity.Base.Organizations.Api.Models;

public sealed class CreateOrganizationRoleRequest
{
    public Guid? OrganizationId { get; init; }

    public string Name { get; init; } = string.Empty;

    public string? Description { get; init; }

    public bool IsSystemRole { get; init; }
}
