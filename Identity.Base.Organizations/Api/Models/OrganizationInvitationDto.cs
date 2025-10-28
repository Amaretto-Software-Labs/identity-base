using System;

namespace Identity.Base.Organizations.Api.Models;

public sealed class OrganizationInvitationDto
{
    public Guid Code { get; init; }

    public Guid OrganizationId { get; init; }

    public string OrganizationSlug { get; init; } = string.Empty;

    public string OrganizationName { get; init; } = string.Empty;

    public string Email { get; init; } = string.Empty;

    public Guid[] RoleIds { get; init; } = Array.Empty<Guid>();

    public Guid? CreatedBy { get; init; }

    public DateTimeOffset CreatedAtUtc { get; init; }

    public DateTimeOffset ExpiresAtUtc { get; init; }

    public DateTimeOffset? UsedAtUtc { get; init; }

    public Guid? UsedByUserId { get; init; }
}
