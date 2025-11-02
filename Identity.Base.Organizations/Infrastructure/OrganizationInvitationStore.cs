using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Identity.Base.Organizations.Abstractions;
using Identity.Base.Organizations.Data;
using Identity.Base.Organizations.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Identity.Base.Organizations.Infrastructure;

public sealed class OrganizationInvitationStore : IOrganizationInvitationStore
{
    private readonly OrganizationDbContext _dbContext;

    public OrganizationInvitationStore(OrganizationDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<OrganizationInvitationRecord> CreateAsync(OrganizationInvitationRecord invitation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(invitation);

        var entity = MapToEntity(invitation);
        _dbContext.Set<OrganizationInvitationEntity>().Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return MapToRecord(entity);
    }

    public async Task<IReadOnlyCollection<OrganizationInvitationRecord>> ListAsync(Guid organizationId, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var entities = await _dbContext.Set<OrganizationInvitationEntity>()
            .AsNoTracking()
            .Where(invitation => invitation.OrganizationId == organizationId && invitation.ExpiresAtUtc > now && invitation.UsedAtUtc == null)
            .OrderBy(invitation => invitation.ExpiresAtUtc)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.Select(MapToRecord).ToList();
    }

    public async Task<OrganizationInvitationRecord?> FindAsync(Guid code, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.Set<OrganizationInvitationEntity>()
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

    public async Task<bool> HasActiveInvitationAsync(Guid organizationId, string normalizedEmail, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        return await _dbContext.Set<OrganizationInvitationEntity>()
            .AsNoTracking()
            .AnyAsync(invitation =>
                invitation.OrganizationId == organizationId &&
                invitation.Email == normalizedEmail &&
                invitation.UsedAtUtc == null &&
                invitation.ExpiresAtUtc > now,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task RemoveInternalAsync(Guid code, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.Set<OrganizationInvitationEntity>()
            .FirstOrDefaultAsync(invitation => invitation.Code == code, cancellationToken)
            .ConfigureAwait(false);

        if (entity is null)
        {
            return;
        }

        _dbContext.Set<OrganizationInvitationEntity>().Remove(entity);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static OrganizationInvitationRecord MapToRecord(OrganizationInvitationEntity entity) => new()
    {
        Code = entity.Code,
        OrganizationId = entity.OrganizationId,
        OrganizationSlug = entity.OrganizationSlug,
        OrganizationName = entity.OrganizationName,
        Email = entity.Email,
        RoleIds = entity.RoleIds ?? Array.Empty<Guid>(),
        CreatedBy = entity.CreatedBy,
        CreatedAtUtc = entity.CreatedAtUtc,
        ExpiresAtUtc = entity.ExpiresAtUtc,
        UsedAtUtc = entity.UsedAtUtc,
        UsedByUserId = entity.UsedByUserId
    };

    private static OrganizationInvitationEntity MapToEntity(OrganizationInvitationRecord record) => new()
    {
        Code = record.Code,
        OrganizationId = record.OrganizationId,
        OrganizationSlug = record.OrganizationSlug,
        OrganizationName = record.OrganizationName,
        Email = record.Email,
        RoleIds = record.RoleIds ?? Array.Empty<Guid>(),
        CreatedBy = record.CreatedBy,
        CreatedAtUtc = record.CreatedAtUtc,
        ExpiresAtUtc = record.ExpiresAtUtc,
        UsedAtUtc = record.UsedAtUtc,
        UsedByUserId = record.UsedByUserId
    };
}
