using System;
using System.Collections.Generic;

namespace OrgSampleApi.Sample.Members;

public sealed class UpdateOrganizationMemberRequest
{
    public IReadOnlyCollection<Guid>? RoleIds { get; init; }
}
