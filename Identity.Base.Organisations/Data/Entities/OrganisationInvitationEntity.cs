using System;

namespace Identity.Base.Organisations.Data.Entities;

public sealed class OrganisationInvitationEntity
{
    public Guid Code { get; set; }

    public Guid OrganisationId { get; set; }

    public string OrganisationSlug { get; set; } = string.Empty;

    public string OrganisationName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public Guid[] RoleIds { get; set; } = Array.Empty<Guid>();

    public Guid? CreatedBy { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset ExpiresAtUtc { get; set; }

    public DateTimeOffset? UsedAtUtc { get; set; }

    public Guid? UsedByUserId { get; set; }
}
