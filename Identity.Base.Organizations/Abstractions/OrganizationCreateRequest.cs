using System;
using Identity.Base.Organizations.Domain;

namespace Identity.Base.Organizations.Abstractions;

public sealed class OrganizationCreateRequest
{
    public Guid? TenantId { get; init; }

    public string Slug { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public OrganizationMetadata? Metadata { get; init; }
}
