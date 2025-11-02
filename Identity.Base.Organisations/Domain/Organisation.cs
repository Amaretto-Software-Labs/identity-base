using System;
using System.Collections.Generic;

namespace Identity.Base.Organisations.Domain;

public class Organisation
{
    public Guid Id { get; set; }

    public Guid? TenantId { get; set; }

    public string Slug { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public OrganisationStatus Status { get; set; } = OrganisationStatus.Active;

    public OrganisationMetadata Metadata { get; set; } = OrganisationMetadata.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public DateTimeOffset? ArchivedAtUtc { get; set; }

    public ICollection<OrganisationMembership> Memberships { get; set; } = new List<OrganisationMembership>();

    public ICollection<OrganisationRole> Roles { get; set; } = new List<OrganisationRole>();
}
