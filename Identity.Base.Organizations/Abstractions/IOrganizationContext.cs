using System;
using Identity.Base.Organizations.Domain;

namespace Identity.Base.Organizations.Abstractions;

public interface IOrganizationContext
{
    bool HasOrganization { get; }

    Guid? OrganizationId { get; }

    Guid? TenantId { get; }

    string? OrganizationSlug { get; }

    string? DisplayName { get; }

    OrganizationMetadata Metadata { get; }

    string? this[string key] { get; }
}
