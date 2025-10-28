using System;

namespace Identity.Base.Organizations.Services;

public sealed class OrganizationInvitationAcceptanceResult
{
    public Guid OrganizationId { get; init; }

    public string OrganizationSlug { get; init; } = string.Empty;

    public string OrganizationName { get; init; } = string.Empty;

    public Guid[] RoleIds { get; init; } = Array.Empty<Guid>();

    public bool WasExistingMember { get; init; }

    public bool WasExistingUser { get; init; }
}
