using System;
using System.Collections.Generic;
using Identity.Base.Organisations.Domain;

namespace Identity.Base.Organisations.Abstractions;

public sealed class OrganisationContext : IOrganisationContext
{
    private static readonly OrganisationMetadata EmptyMetadata = OrganisationMetadata.Empty;

    public static OrganisationContext None { get; } = new(null, null, null, null, null);

    public OrganisationContext(
        Guid? organisationId,
        Guid? tenantId,
        string? organisationSlug,
        string? displayName,
        OrganisationMetadata? metadata)
    {
        OrganisationId = organisationId;
        TenantId = tenantId;
        OrganisationSlug = organisationSlug;
        DisplayName = displayName;
        Metadata = metadata ?? EmptyMetadata;
    }

    public bool HasOrganisation => OrganisationId.HasValue || !string.IsNullOrWhiteSpace(OrganisationSlug);

    public Guid? OrganisationId { get; }

    public Guid? TenantId { get; }

    public string? OrganisationSlug { get; }

    public string? DisplayName { get; }

    public OrganisationMetadata Metadata { get; }

    public string? this[string key]
    {
        get
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return null;
            }

            return Metadata.Values.TryGetValue(key, out var value) ? value : null;
        }
    }
}
