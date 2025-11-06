using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Identity.Base.Abstractions.Pagination;

namespace Identity.Base.Organizations.Abstractions;

public interface IOrganizationInvitationStore
{
    Task<OrganizationInvitationRecord> CreateAsync(OrganizationInvitationRecord invitation, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<OrganizationInvitationRecord>> ListAsync(Guid organizationId, CancellationToken cancellationToken = default);

    Task<PagedResult<OrganizationInvitationRecord>> ListAsync(
        Guid organizationId,
        PageRequest pageRequest,
        CancellationToken cancellationToken = default);

    Task<OrganizationInvitationRecord?> FindAsync(Guid code, CancellationToken cancellationToken = default);

    Task RemoveAsync(Guid code, CancellationToken cancellationToken = default);

    Task<bool> HasActiveInvitationAsync(Guid organizationId, string normalizedEmail, CancellationToken cancellationToken = default);
}
