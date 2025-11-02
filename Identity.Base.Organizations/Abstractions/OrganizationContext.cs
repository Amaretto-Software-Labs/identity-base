using System;
using System.Collections.Generic;
using Identity.Base.Organizations.Domain;

namespace Identity.Base.Organizations.Abstractions;

public sealed class OrganizationContext : IOrganizationContext
{
    private static readonly OrganizationMetadata EmptyMetadata = OrganizationMetadata.Empty;

    public static OrganizationContext None { get; } = new(null, null, null, null, null);

    public OrganizationContext(
        Guid? organizationId,
        Guid? tenantId,
        string? organizationSlug,
        string? displayName,
        OrganizationMetadata? metadata)
    {
        OrganizationId = organizationId;
        TenantId = tenantId;
        OrganizationSlug = organizationSlug;
        DisplayName = displayName;
        Metadata = metadata ?? EmptyMetadata;
    }

    public bool HasOrganization => OrganizationId.HasValue || !string.IsNullOrWhiteSpace(OrganizationSlug);

    public Guid? OrganizationId { get; }

    public Guid? TenantId { get; }

    public string? OrganizationSlug { get; }

    public string? DisplayName { get; }

    public OrganizationMetadata Metadata { get; }

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
