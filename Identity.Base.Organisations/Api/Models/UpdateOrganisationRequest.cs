using Identity.Base.Organisations.Domain;

namespace Identity.Base.Organisations.Api.Models;

public sealed class UpdateOrganisationRequest
{
    public string? DisplayName { get; init; }

    public OrganisationMetadata? Metadata { get; init; }

    public OrganisationStatus? Status { get; init; }
}
