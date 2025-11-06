using System;
using System.Collections.Generic;
using Identity.Base.Abstractions.Pagination;
using Identity.Base.Organizations.Domain;

namespace Identity.Base.Organizations.Api.Models;

public sealed record AdminOrganizationListQuery(
    Guid? TenantId = null,
    int? Page = null,
    int? PageSize = null,
    string? Search = null,
    IEnumerable<string>? Sort = null,
    OrganizationStatus? Status = null)
{
    public PageRequest ToPageRequest(int defaultPageSize = 25, int maxPageSize = 200)
        => PageRequest.Create(Page, PageSize, Search, Sort, defaultPageSize, maxPageSize);
}
