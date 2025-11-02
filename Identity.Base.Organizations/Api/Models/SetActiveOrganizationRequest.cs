using System;

namespace Identity.Base.Organizations.Api.Models;

public sealed class SetActiveOrganizationRequest
{
    public Guid OrganizationId { get; init; }

    public string? OrganizationSlug { get; init; }
}
