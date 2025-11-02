using System;
using System.Collections.Generic;

namespace Identity.Base.Organisations.Api.Models;

public sealed class OrganisationRolePermissionsResponse
{
    public IReadOnlyList<string> Effective { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> Explicit { get; init; } = Array.Empty<string>();
}
