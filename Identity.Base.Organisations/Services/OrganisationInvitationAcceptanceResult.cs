using System;

namespace Identity.Base.Organisations.Services;

public sealed class OrganisationInvitationAcceptanceResult
{
    public Guid OrganisationId { get; init; }

    public string OrganisationSlug { get; init; } = string.Empty;

    public string OrganisationName { get; init; } = string.Empty;

    public Guid[] RoleIds { get; init; } = Array.Empty<Guid>();

    public bool WasExistingMember { get; init; }

    public bool WasExistingUser { get; init; }
}
