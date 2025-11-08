using System;
using System.Collections.Generic;
using Identity.Base.Organizations.Domain;

namespace Identity.Base.Organizations.Api.Models;

public sealed record UserOrganizationMembershipDto(
    Guid OrganizationId,
    Guid? TenantId,
    string Slug,
    string DisplayName,
    OrganizationStatus Status,
    IReadOnlyList<Guid> RoleIds,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc);
