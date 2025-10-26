using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace OrgSampleApi.Sample.Invitations;

public interface IInvitationStore
{
    Task<InvitationRecord> CreateAsync(InvitationRecord invitation, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<InvitationRecord>> ListAsync(Guid organizationId, CancellationToken cancellationToken = default);

    Task<InvitationRecord?> FindAsync(Guid code, CancellationToken cancellationToken = default);

    Task RemoveAsync(Guid code, CancellationToken cancellationToken = default);
}

