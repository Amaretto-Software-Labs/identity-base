using System;
using System.Collections.Generic;

namespace Identity.Base.Organisations.Abstractions;

public sealed class OrganisationMembershipRequest
{
    public Guid OrganisationId { get; init; }

    public Guid UserId { get; init; }

    public Guid? TenantId { get; init; }

    public bool IsPrimary { get; init; }

    public IReadOnlyCollection<Guid> RoleIds { get; init; } = Array.Empty<Guid>();
}
