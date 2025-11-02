using System;

namespace Identity.Base.Organisations.Api.Models;

public sealed class SetActiveOrganisationRequest
{
    public Guid OrganisationId { get; init; }

    public string? OrganisationSlug { get; init; }
}
