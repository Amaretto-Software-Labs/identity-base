using Identity.Base.Organizations.Domain;

namespace Identity.Base.Organizations.Api.Models;

public sealed class CreateOrganizationRequest
{
    public Guid? TenantId { get; init; }

    public string Slug { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public OrganizationMetadata? Metadata { get; init; }
}
