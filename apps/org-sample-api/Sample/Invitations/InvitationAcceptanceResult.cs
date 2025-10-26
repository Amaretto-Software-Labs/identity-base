using System;

namespace OrgSampleApi.Sample.Invitations;

public sealed class InvitationAcceptanceResult
{
    public Guid OrganizationId { get; init; }

    public string OrganizationSlug { get; init; } = string.Empty;

    public string OrganizationName { get; init; } = string.Empty;

    public Guid[] RoleIds { get; init; } = Array.Empty<Guid>();

    public bool WasExistingMember { get; init; }
}

