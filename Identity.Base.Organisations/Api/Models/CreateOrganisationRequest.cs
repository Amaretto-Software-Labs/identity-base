using Identity.Base.Organisations.Domain;

namespace Identity.Base.Organisations.Api.Models;

public sealed class CreateOrganisationRequest
{
    public Guid? TenantId { get; init; }

    public string Slug { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public OrganisationMetadata? Metadata { get; init; }
}
