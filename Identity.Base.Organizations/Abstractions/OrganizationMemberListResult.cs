using System;
using System.Collections.Generic;

namespace Identity.Base.Organizations.Abstractions;

public sealed class OrganizationMemberListResult
{
    public OrganizationMemberListResult(int page, int pageSize, int totalCount, IReadOnlyList<OrganizationMemberListItem> members)
    {
        if (page < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(page), "Page must be greater than zero.");
        }

        if (pageSize < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(pageSize), "Page size must be greater than zero.");
        }

        Page = page;
        PageSize = pageSize;
        TotalCount = totalCount < 0 ? 0 : totalCount;
        Members = members ?? throw new ArgumentNullException(nameof(members));
    }

    public int Page { get; }

    public int PageSize { get; }

    public int TotalCount { get; }

    public IReadOnlyList<OrganizationMemberListItem> Members { get; }

    public static OrganizationMemberListResult Empty(int page, int pageSize)
        => new(page, pageSize, 0, Array.Empty<OrganizationMemberListItem>());
}
