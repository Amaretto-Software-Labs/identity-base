using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Identity.Base.Abstractions.Pagination;
using Identity.Base.Organizations.Domain;

namespace Identity.Base.Organizations.Abstractions;

public interface IOrganizationService
{
    Task<Organization> CreateAsync(OrganizationCreateRequest request, CancellationToken cancellationToken = default);

    Task<Organization?> GetByIdAsync(Guid organizationId, CancellationToken cancellationToken = default);

    Task<Organization?> GetBySlugAsync(Guid? tenantId, string slug, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Organization>> ListAsync(Guid? tenantId, CancellationToken cancellationToken = default);

    Task<PagedResult<Organization>> ListAsync(
        Guid? tenantId,
        PageRequest pageRequest,
        OrganizationStatus? status = null,
        CancellationToken cancellationToken = default);

    Task<Organization> UpdateAsync(Guid organizationId, OrganizationUpdateRequest request, CancellationToken cancellationToken = default);

    Task ArchiveAsync(Guid organizationId, CancellationToken cancellationToken = default);
}
