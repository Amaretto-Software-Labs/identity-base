using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Identity.Base.Organizations.Domain;

namespace Identity.Base.Organizations.Abstractions;

public interface IOrganizationRoleService
{
    Task<OrganizationRole> CreateAsync(OrganizationRoleCreateRequest request, CancellationToken cancellationToken = default);

    Task<OrganizationRole?> GetByIdAsync(Guid roleId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OrganizationRole>> ListAsync(Guid? tenantId, Guid? organizationId, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid roleId, CancellationToken cancellationToken = default);
}
