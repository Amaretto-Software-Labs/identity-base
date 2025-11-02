using System;
using System.Collections.Generic;

namespace Identity.Base.Organizations.Api.Models;

public sealed class OrganizationMemberListResponse
{
    public int Page { get; init; }

    public int PageSize { get; init; }

    public int TotalCount { get; init; }

    public IReadOnlyCollection<OrganizationMembershipDto> Members { get; init; } = Array.Empty<OrganizationMembershipDto>();
}
