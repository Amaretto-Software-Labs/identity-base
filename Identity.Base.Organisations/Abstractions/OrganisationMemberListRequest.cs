using System;

namespace Identity.Base.Organisations.Abstractions;

public sealed class OrganisationMemberListRequest
{
    public Guid OrganisationId { get; init; }

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 25;

    public string? Search { get; init; }

    public Guid? RoleId { get; init; }

    public bool? IsPrimary { get; init; }

    public OrganisationMemberSort Sort { get; init; } = OrganisationMemberSort.CreatedAtDescending;
}

public enum OrganisationMemberSort
{
    CreatedAtAscending,
    CreatedAtDescending
}
