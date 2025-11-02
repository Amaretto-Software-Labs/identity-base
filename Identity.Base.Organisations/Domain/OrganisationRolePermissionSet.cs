using System.Collections.Generic;

namespace Identity.Base.Organisations.Domain;

public sealed record OrganisationRolePermissionSet(
    IReadOnlyList<string> Effective,
    IReadOnlyList<string> Explicit);
