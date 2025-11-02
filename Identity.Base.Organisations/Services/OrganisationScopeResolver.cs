using System;
using System.Threading;
using System.Threading.Tasks;
using Identity.Base.Organisations.Abstractions;
using Identity.Base.Organisations.Data;
using Microsoft.EntityFrameworkCore;

namespace Identity.Base.Organisations.Services;

public class OrganisationScopeResolver : IOrganisationScopeResolver
{
    private readonly OrganisationDbContext _dbContext;

    public OrganisationScopeResolver(OrganisationDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public virtual Task<bool> IsInScopeAsync(Guid userId, Guid organisationId, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            return Task.FromResult(false);
        }

        if (organisationId == Guid.Empty)
        {
            return Task.FromResult(true);
        }

        return _dbContext.OrganisationMemberships
            .AsNoTracking()
            .AnyAsync(membership => membership.OrganisationId == organisationId && membership.UserId == userId, cancellationToken);
    }
}
