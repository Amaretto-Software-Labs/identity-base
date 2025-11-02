using Identity.Base.Organizations.Domain;

namespace Identity.Base.Organizations.Abstractions;

public sealed class OrganizationUpdateRequest
{
    public string? DisplayName { get; init; }

    public OrganizationMetadata? Metadata { get; init; }

    public OrganizationStatus? Status { get; init; }
}
