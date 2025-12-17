using System;

namespace Identity.Base.Organizations.Api.Models;

public sealed class OrganizationInvitationPreviewDto
{
    public Guid Code { get; init; }

    public string OrganizationSlug { get; init; } = string.Empty;

    public string OrganizationName { get; init; } = string.Empty;

    public DateTimeOffset ExpiresAtUtc { get; init; }
}

