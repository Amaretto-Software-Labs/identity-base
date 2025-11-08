using System;

namespace Identity.Base.Organizations.Api.Models;

public sealed record OrganizationMemberListQuery(
    int Page = 1,
    int PageSize = 25,
    string? Search = null,
    Guid? RoleId = null,
    string? Sort = null);
