using Identity.Base.Organizations.Domain;

namespace Identity.Base.Organizations.Api.Models;

public sealed class UpdateOrganizationRequest
{
    public string? DisplayName { get; init; }

    public OrganizationMetadata? Metadata { get; init; }

    public OrganizationStatus? Status { get; init; }
}
