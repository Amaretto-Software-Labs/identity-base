using System;

namespace Identity.Base.Organisations.Api.Models;

public sealed class ActiveOrganisationResponse
{
    public required OrganisationDto Organisation { get; init; }

    public Guid[] RoleIds { get; init; } = Array.Empty<Guid>();

    public bool RequiresTokenRefresh { get; init; } = true;
}
