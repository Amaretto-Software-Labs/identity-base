using System.Collections.Generic;
using Identity.Base.Abstractions.Pagination;

namespace Identity.Base.Organizations.Api.Models;

public sealed record OrganizationInvitationListQuery(
    int? Page = null,
    int? PageSize = null,
    string? Search = null,
    string? Sort = null)
{
    public PageRequest ToPageRequest(int defaultPageSize = 25, int maxPageSize = 200)
        => PageRequest.Create(Page, PageSize, Search, Sort is null ? null : new[] { Sort }, defaultPageSize, maxPageSize);
}
