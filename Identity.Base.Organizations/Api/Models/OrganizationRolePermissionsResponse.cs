using System;
using System.Collections.Generic;

namespace Identity.Base.Organizations.Api.Models;

public sealed class OrganizationRolePermissionsResponse
{
    public IReadOnlyList<string> Effective { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> Explicit { get; init; } = Array.Empty<string>();
}
