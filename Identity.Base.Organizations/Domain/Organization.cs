using System;
using System.Collections.Generic;

namespace Identity.Base.Organizations.Domain;

public class Organization
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public string Slug { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public OrganizationStatus Status { get; set; } = OrganizationStatus.Active;

    public OrganizationMetadata Metadata { get; set; } = OrganizationMetadata.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public DateTimeOffset? ArchivedAtUtc { get; set; }

    public ICollection<OrganizationMembership> Memberships { get; set; } = new List<OrganizationMembership>();

    public ICollection<OrganizationRole> Roles { get; set; } = new List<OrganizationRole>();
}
