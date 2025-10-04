using System;
using System.Collections.Generic;

namespace Identity.Base.Organizations.Abstractions;

public sealed class OrganizationMembershipRequest
{
    public Guid OrganizationId { get; init; }

    public Guid UserId { get; init; }

    public Guid? TenantId { get; init; }

    public bool IsPrimary { get; init; }

    public IReadOnlyCollection<Guid> RoleIds { get; init; } = Array.Empty<Guid>();
}
