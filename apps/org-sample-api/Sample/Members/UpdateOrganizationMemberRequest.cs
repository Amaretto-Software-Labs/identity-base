using System;
using System.Collections.Generic;

namespace OrgSampleApi.Sample.Members;

public sealed class UpdateOrganizationMemberRequest
{
    public bool? IsPrimary { get; init; }

    public IReadOnlyCollection<Guid>? RoleIds { get; init; }
}
