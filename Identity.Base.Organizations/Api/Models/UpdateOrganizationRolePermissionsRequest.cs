using System;
using System.Collections.Generic;

namespace Identity.Base.Organizations.Api.Models;

public sealed class UpdateOrganizationRolePermissionsRequest
{
    public IEnumerable<string> Permissions { get; init; } = Array.Empty<string>();
}
