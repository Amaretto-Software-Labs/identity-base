namespace Identity.Base.Organisations.Api.Models;

public sealed class CreateOrganisationRoleRequest
{
    public Guid? OrganisationId { get; init; }

    public string Name { get; init; } = string.Empty;

    public string? Description { get; init; }

    public bool IsSystemRole { get; init; }
}
