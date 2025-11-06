using System.Collections.Generic;

namespace Identity.Base.Abstractions.Pagination;

public sealed record PagedResult<T>(int Page, int PageSize, int TotalCount, IReadOnlyList<T> Items)
{
    public static PagedResult<T> Empty(int page, int pageSize)
        => new(page, pageSize, 0, new List<T>(0));
}
