using System;

namespace Identity.Base.Organizations.Api.Models;

public sealed class ActiveOrganizationResponse
{
    public required OrganizationDto Organization { get; init; }

    public Guid[] RoleIds { get; init; } = Array.Empty<Guid>();

    public bool RequiresTokenRefresh { get; init; } = true;
}
