using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Identity.Base.Organisations.Domain;

namespace Identity.Base.Organisations.Abstractions;

public interface IOrganisationService
{
    Task<Organisation> CreateAsync(OrganisationCreateRequest request, CancellationToken cancellationToken = default);

    Task<Organisation?> GetByIdAsync(Guid organisationId, CancellationToken cancellationToken = default);

    Task<Organisation?> GetBySlugAsync(Guid? tenantId, string slug, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Organisation>> ListAsync(Guid? tenantId, CancellationToken cancellationToken = default);

    Task<Organisation> UpdateAsync(Guid organisationId, OrganisationUpdateRequest request, CancellationToken cancellationToken = default);

    Task ArchiveAsync(Guid organisationId, CancellationToken cancellationToken = default);
}
