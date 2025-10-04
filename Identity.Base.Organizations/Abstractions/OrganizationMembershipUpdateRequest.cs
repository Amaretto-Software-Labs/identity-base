using System;
using System.Collections.Generic;

namespace Identity.Base.Organizations.Abstractions;

public sealed class OrganizationMembershipUpdateRequest
{
    public Guid OrganizationId { get; init; }

    public Guid UserId { get; init; }

    public bool? IsPrimary { get; init; }

    public IReadOnlyCollection<Guid>? RoleIds { get; init; }
}
