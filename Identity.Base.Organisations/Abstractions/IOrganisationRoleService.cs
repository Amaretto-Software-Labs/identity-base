using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Identity.Base.Organisations.Domain;

namespace Identity.Base.Organisations.Abstractions;

public interface IOrganisationRoleService
{
    Task<OrganisationRole> CreateAsync(OrganisationRoleCreateRequest request, CancellationToken cancellationToken = default);

    Task<OrganisationRole?> GetByIdAsync(Guid roleId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OrganisationRole>> ListAsync(Guid? tenantId, Guid? organisationId, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid roleId, CancellationToken cancellationToken = default);

    Task<OrganisationRolePermissionSet> GetPermissionsAsync(Guid roleId, Guid organisationId, CancellationToken cancellationToken = default);

    Task UpdatePermissionsAsync(Guid roleId, Guid organisationId, IEnumerable<string> permissions, CancellationToken cancellationToken = default);
}
