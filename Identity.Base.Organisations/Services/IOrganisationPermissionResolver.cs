using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Identity.Base.Organisations.Services;

public interface IOrganisationPermissionResolver
{
    Task<IReadOnlyList<string>> GetPermissionsAsync(Guid organisationId, Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> GetOrganisationPermissionsAsync(Guid organisationId, Guid userId, CancellationToken cancellationToken = default);
}
