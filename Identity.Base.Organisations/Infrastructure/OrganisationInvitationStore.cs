using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Identity.Base.Organisations.Abstractions;
using Identity.Base.Organisations.Data;
using Identity.Base.Organisations.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Identity.Base.Organisations.Infrastructure;

public sealed class OrganisationInvitationStore : IOrganisationInvitationStore
{
    private readonly OrganisationDbContext _dbContext;

    public OrganisationInvitationStore(OrganisationDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<OrganisationInvitationRecord> CreateAsync(OrganisationInvitationRecord invitation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(invitation);

        var entity = MapToEntity(invitation);
        _dbContext.Set<OrganisationInvitationEntity>().Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return MapToRecord(entity);
    }

    public async Task<IReadOnlyCollection<OrganisationInvitationRecord>> ListAsync(Guid organisationId, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var entities = await _dbContext.Set<OrganisationInvitationEntity>()
            .AsNoTracking()
            .Where(invitation => invitation.OrganisationId == organisationId && invitation.ExpiresAtUtc > now && invitation.UsedAtUtc == null)
            .OrderBy(invitation => invitation.ExpiresAtUtc)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.Select(MapToRecord).ToList();
    }

    public async Task<OrganisationInvitationRecord?> FindAsync(Guid code, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.Set<OrganisationInvitationEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(invitation => invitation.Code == code, cancellationToken)
            .ConfigureAwait(false);

        if (entity is null)
        {
            return null;
        }

        if (entity.UsedAtUtc is not null)
        {
            return null;
        }

        if (entity.ExpiresAtUtc <= DateTimeOffset.UtcNow)
        {
            await RemoveInternalAsync(code, cancellationToken).ConfigureAwait(false);
            return null;
        }

        return MapToRecord(entity);
    }

    public async Task RemoveAsync(Guid code, CancellationToken cancellationToken = default)
    {
        await RemoveInternalAsync(code, cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> HasActiveInvitationAsync(Guid organisationId, string normalizedEmail, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        return await _dbContext.Set<OrganisationInvitationEntity>()
            .AsNoTracking()
            .AnyAsync(invitation =>
                invitation.OrganisationId == organisationId &&
                invitation.Email == normalizedEmail &&
                invitation.UsedAtUtc == null &&
                invitation.ExpiresAtUtc > now,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task RemoveInternalAsync(Guid code, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.Set<OrganisationInvitationEntity>()
            .FirstOrDefaultAsync(invitation => invitation.Code == code, cancellationToken)
            .ConfigureAwait(false);

        if (entity is null)
        {
            return;
        }

        _dbContext.Set<OrganisationInvitationEntity>().Remove(entity);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static OrganisationInvitationRecord MapToRecord(OrganisationInvitationEntity entity) => new()
    {
        Code = entity.Code,
        OrganisationId = entity.OrganisationId,
        OrganisationSlug = entity.OrganisationSlug,
        OrganisationName = entity.OrganisationName,
        Email = entity.Email,
        RoleIds = entity.RoleIds ?? Array.Empty<Guid>(),
        CreatedBy = entity.CreatedBy,
        CreatedAtUtc = entity.CreatedAtUtc,
        ExpiresAtUtc = entity.ExpiresAtUtc,
        UsedAtUtc = entity.UsedAtUtc,
        UsedByUserId = entity.UsedByUserId
    };

    private static OrganisationInvitationEntity MapToEntity(OrganisationInvitationRecord record) => new()
    {
        Code = record.Code,
        OrganisationId = record.OrganisationId,
        OrganisationSlug = record.OrganisationSlug,
        OrganisationName = record.OrganisationName,
        Email = record.Email,
        RoleIds = record.RoleIds ?? Array.Empty<Guid>(),
        CreatedBy = record.CreatedBy,
        CreatedAtUtc = record.CreatedAtUtc,
        ExpiresAtUtc = record.ExpiresAtUtc,
        UsedAtUtc = record.UsedAtUtc,
        UsedByUserId = record.UsedByUserId
    };
}
