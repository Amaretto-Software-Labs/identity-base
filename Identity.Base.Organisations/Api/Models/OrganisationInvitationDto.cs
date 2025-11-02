using System;

namespace Identity.Base.Organisations.Api.Models;

public sealed class OrganisationInvitationDto
{
    public Guid Code { get; init; }

    public Guid OrganisationId { get; init; }

    public string OrganisationSlug { get; init; } = string.Empty;

    public string OrganisationName { get; init; } = string.Empty;

    public string Email { get; init; } = string.Empty;

    public Guid[] RoleIds { get; init; } = Array.Empty<Guid>();

    public Guid? CreatedBy { get; init; }

    public DateTimeOffset CreatedAtUtc { get; init; }

    public DateTimeOffset ExpiresAtUtc { get; init; }

    public DateTimeOffset? UsedAtUtc { get; init; }

    public Guid? UsedByUserId { get; init; }
}
