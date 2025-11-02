using System;

namespace Identity.Base.Organizations.Abstractions;

public sealed class OrganizationMemberListRequest
{
    public Guid OrganizationId { get; init; }

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 25;

    public string? Search { get; init; }

    public Guid? RoleId { get; init; }

    public bool? IsPrimary { get; init; }

    public OrganizationMemberSort Sort { get; init; } = OrganizationMemberSort.CreatedAtDescending;
}

public enum OrganizationMemberSort
{
    CreatedAtAscending,
    CreatedAtDescending
}
