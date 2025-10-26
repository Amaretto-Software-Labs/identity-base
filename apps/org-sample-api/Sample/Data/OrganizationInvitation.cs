using System;

namespace OrgSampleApi.Sample.Data;

public sealed class OrganizationInvitation
{
    public Guid Code { get; set; }

    public Guid OrganizationId { get; set; }

    public string OrganizationSlug { get; set; } = string.Empty;

    public string OrganizationName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public Guid[] RoleIds { get; set; } = Array.Empty<Guid>();

    public Guid? CreatedBy { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset ExpiresAtUtc { get; set; }
}

