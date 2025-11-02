using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Identity.Base.Organizations.Services;

public interface IOrganizationPermissionResolver
{
    Task<IReadOnlyList<string>> GetPermissionsAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> GetOrganizationPermissionsAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken = default);
}
