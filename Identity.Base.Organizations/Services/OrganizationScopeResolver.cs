using System;
using System.Threading;
using System.Threading.Tasks;
using Identity.Base.Organizations.Abstractions;

namespace Identity.Base.Organizations.Services;

public class OrganizationScopeResolver : IOrganizationScopeResolver
{
    public virtual Task<bool> IsInScopeAsync(Guid userId, Guid organizationId, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            return Task.FromResult(false);
        }

        if (organizationId == Guid.Empty)
        {
            return Task.FromResult(true);
        }

        return Task.FromResult(true);
    }
}
