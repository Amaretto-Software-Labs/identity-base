using System;
using System.Collections.Generic;
using System.Linq;

namespace Identity.Base.Abstractions.Pagination;

public sealed class PageRequest
{
    public int Page { get; }
    public int PageSize { get; }
    public string? Search { get; }
    public IReadOnlyList<SortExpression> Sorts { get; }

    private PageRequest(int page, int pageSize, string? search, IReadOnlyList<SortExpression> sorts)
    {
        Page = page;
        PageSize = pageSize;
        Search = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        Sorts = sorts;
    }

    public static PageRequest Create(
        int? page,
        int? pageSize,
        string? search,
        IEnumerable<string>? sortValues,
        int defaultPageSize = 25,
        int maxPageSize = 200)
    {
        var normalizedPage = NormalizePage(page);
        var normalizedPageSize = NormalizePageSize(pageSize, defaultPageSize, maxPageSize);

        var sorts = ParseSorts(sortValues);

        return new PageRequest(normalizedPage, normalizedPageSize, search, sorts);
    }

    public static PageRequest Create(
        int page,
        int pageSize,
        string? search,
        IEnumerable<SortExpression>? sorts)
        => new(
            NormalizePage(page),
            NormalizePageSize(pageSize, 25, 200),
            search,
            sorts?.ToArray() ?? Array.Empty<SortExpression>());

    public PageRequest WithDefaults(int defaultPageSize = 25, int maxPageSize = 200)
        => new(
            NormalizePage(Page),
            NormalizePageSize(PageSize, defaultPageSize, maxPageSize),
            Search,
            Sorts);

    public int GetSkip() => (Page - 1) * PageSize;

    private static int NormalizePage(int? page)
    {
        if (!page.HasValue || page.Value < 1)
        {
            return 1;
        }

        return page.Value;
    }

    private static int NormalizePageSize(int? pageSize, int defaultPageSize, int maxPageSize)
    {
        var size = pageSize.HasValue ? pageSize.Value : defaultPageSize;
        if (size < 1)
        {
            size = defaultPageSize;
        }

        if (size > maxPageSize)
        {
            size = maxPageSize;
        }

        return size;
    }

    private static IReadOnlyList<SortExpression> ParseSorts(IEnumerable<string>? sortValues)
    {
        if (sortValues is null)
        {
            return Array.Empty<SortExpression>();
        }

        var list = new List<SortExpression>();
        foreach (var sortValue in sortValues)
        {
            if (string.IsNullOrWhiteSpace(sortValue))
            {
                continue;
            }

            var tokens = sortValue.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var token in tokens)
            {
                if (SortExpression.TryParse(token, out var expression))
                {
                    list.Add(expression);
                }
            }
        }

        return list;
    }
}
