using System.Collections.Generic;

namespace Identity.Base.Abstractions.Pagination;

public sealed record PageQueryParameters(
    int? Page = null,
    int? PageSize = null,
    string? Search = null,
    IEnumerable<string>? Sort = null)
{
    public PageRequest ToPageRequest(int defaultPageSize = 25, int maxPageSize = 200)
        => PageRequest.Create(Page, PageSize, Search, Sort, defaultPageSize, maxPageSize);
}
