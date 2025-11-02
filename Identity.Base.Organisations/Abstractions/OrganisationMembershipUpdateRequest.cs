using System;
using System.Collections.Generic;

namespace Identity.Base.Organisations.Abstractions;

public sealed class OrganisationMembershipUpdateRequest
{
    public Guid OrganisationId { get; init; }

    public Guid UserId { get; init; }

    public bool? IsPrimary { get; init; }

    public IReadOnlyCollection<Guid>? RoleIds { get; init; }
}
