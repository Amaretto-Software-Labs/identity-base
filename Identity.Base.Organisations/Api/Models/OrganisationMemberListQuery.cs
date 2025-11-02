using System;

namespace Identity.Base.Organisations.Api.Models;

public sealed record OrganisationMemberListQuery(
    int Page = 1,
    int PageSize = 25,
    string? Search = null,
    Guid? RoleId = null,
    bool? IsPrimary = null,
    string? Sort = null);
