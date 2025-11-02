using System;
using System.Threading;
using System.Threading.Tasks;

namespace Identity.Base.Organisations.Abstractions;

public interface IOrganisationScopeResolver
{
    Task<bool> IsInScopeAsync(Guid userId, Guid organisationId, CancellationToken cancellationToken = default);
}
