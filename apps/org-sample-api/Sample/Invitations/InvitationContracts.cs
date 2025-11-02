using System;
using System.Collections.Generic;

namespace OrgSampleApi.Sample.Invitations;

public sealed class CreateInvitationRequest
{
    public string Email { get; set; } = string.Empty;

    public IReadOnlyCollection<Guid> RoleIds { get; set; } = Array.Empty<Guid>();

    public int? ExpiresInHours { get; set; }
}

public sealed class InvitationResponse
{
    public Guid Code { get; init; }

    public string Email { get; init; } = string.Empty;

    public Guid[] RoleIds { get; init; } = Array.Empty<Guid>();

    public DateTimeOffset ExpiresAtUtc { get; init; }

    public string OrganisationName { get; init; } = string.Empty;

    public string OrganisationSlug { get; init; } = string.Empty;

    public bool IsExistingUser { get; init; }

    public string? RegisterUrl { get; init; }

    public string? ClaimUrl { get; init; }
}

public sealed class ClaimInvitationRequest
{
    public Guid Code { get; set; }
}
