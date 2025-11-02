using System;
using Identity.Base.Organisations.Domain;

namespace Identity.Base.Organisations.Abstractions;

public interface IOrganisationContext
{
    bool HasOrganisation { get; }

    Guid? OrganisationId { get; }

    Guid? TenantId { get; }

    string? OrganisationSlug { get; }

    string? DisplayName { get; }

    OrganisationMetadata Metadata { get; }

    string? this[string key] { get; }
}
