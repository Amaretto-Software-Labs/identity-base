using System.Collections.Generic;

namespace Identity.Base.Organizations.Domain;

public sealed record OrganizationRolePermissionSet(
    IReadOnlyList<string> Effective,
    IReadOnlyList<string> Explicit);
