using System;
using System.Threading;
using System.Threading.Tasks;

namespace Identity.Base.Organizations.Abstractions;

public interface IOrganizationScopeResolver
{
    Task<bool> IsInScopeAsync(Guid userId, Guid organizationId, CancellationToken cancellationToken = default);
}
