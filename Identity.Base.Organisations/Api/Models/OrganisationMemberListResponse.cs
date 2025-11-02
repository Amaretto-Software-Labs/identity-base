using System;
using System.Collections.Generic;

namespace Identity.Base.Organisations.Api.Models;

public sealed class OrganisationMemberListResponse
{
    public int Page { get; init; }

    public int PageSize { get; init; }

    public int TotalCount { get; init; }

    public IReadOnlyCollection<OrganisationMembershipDto> Members { get; init; } = Array.Empty<OrganisationMembershipDto>();
}
