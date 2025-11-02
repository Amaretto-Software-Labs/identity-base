using System;
using Identity.Base.Organisations.Domain;

namespace Identity.Base.Organisations.Abstractions;

public sealed class OrganisationCreateRequest
{
    public Guid? TenantId { get; init; }

    public string Slug { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public OrganisationMetadata? Metadata { get; init; }
}
