using System;
using System.Collections.Generic;

namespace OrgSampleApi.Sample.Members;

public sealed class OrganizationMemberDetail
{
    public Guid OrganizationId { get; init; }

    public Guid UserId { get; init; }

    public IReadOnlyCollection<Guid> RoleIds { get; init; } = Array.Empty<Guid>();

    public DateTimeOffset CreatedAtUtc { get; init; }

    public DateTimeOffset? UpdatedAtUtc { get; init; }

    public string? Email { get; init; }

    public string? DisplayName { get; init; }
}
