using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Identity.Base.Organisations.Abstractions;

public interface IOrganisationInvitationStore
{
    Task<OrganisationInvitationRecord> CreateAsync(OrganisationInvitationRecord invitation, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<OrganisationInvitationRecord>> ListAsync(Guid organisationId, CancellationToken cancellationToken = default);

    Task<OrganisationInvitationRecord?> FindAsync(Guid code, CancellationToken cancellationToken = default);

    Task RemoveAsync(Guid code, CancellationToken cancellationToken = default);

    Task<bool> HasActiveInvitationAsync(Guid organisationId, string normalizedEmail, CancellationToken cancellationToken = default);
}
