using System;
using System.Collections.Generic;

namespace Identity.Base.Organizations.Abstractions;

public sealed class OrganizationMemberListItem
{
    public Guid OrganizationId { get; init; }

    public Guid UserId { get; init; }

    public Guid? TenantId { get; init; }

    public IReadOnlyCollection<Guid> RoleIds { get; init; } = Array.Empty<Guid>();

    public DateTimeOffset CreatedAtUtc { get; init; }

    public DateTimeOffset? UpdatedAtUtc { get; init; }

    public string? Email { get; init; }

    public string? DisplayName { get; init; }
}
