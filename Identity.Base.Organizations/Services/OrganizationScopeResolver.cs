using System;
using System.Threading;
using System.Threading.Tasks;
using Identity.Base.Organizations.Abstractions;
using Identity.Base.Organizations.Data;
using Microsoft.EntityFrameworkCore;

namespace Identity.Base.Organizations.Services;

public class OrganizationScopeResolver : IOrganizationScopeResolver
{
    private readonly OrganizationDbContext _dbContext;

    public OrganizationScopeResolver(OrganizationDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

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

        return _dbContext.OrganizationMemberships
            .AsNoTracking()
            .AnyAsync(membership => membership.OrganizationId == organizationId && membership.UserId == userId, cancellationToken);
    }
}
