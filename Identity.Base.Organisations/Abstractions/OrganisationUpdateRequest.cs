using Identity.Base.Organisations.Domain;

namespace Identity.Base.Organisations.Abstractions;

public sealed class OrganisationUpdateRequest
{
    public string? DisplayName { get; init; }

    public OrganisationMetadata? Metadata { get; init; }

    public OrganisationStatus? Status { get; init; }
}
