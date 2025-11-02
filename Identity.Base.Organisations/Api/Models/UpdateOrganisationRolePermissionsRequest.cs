using System;
using System.Collections.Generic;

namespace Identity.Base.Organisations.Api.Models;

public sealed class UpdateOrganisationRolePermissionsRequest
{
    public IEnumerable<string> Permissions { get; init; } = Array.Empty<string>();
}
