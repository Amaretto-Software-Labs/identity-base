using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using OrgSampleApi.Sample.Data;

namespace OrgSampleApi.Sample.Invitations;

public sealed class EfInvitationStore : IInvitationStore
{
    private readonly OrgSampleDbContext _dbContext;

    public EfInvitationStore(OrgSampleDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<InvitationRecord> CreateAsync(InvitationRecord invitation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(invitation);

        var entity = MapToEntity(invitation);
        _dbContext.Invitations.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return MapToRecord(entity);
    }

    public async Task<IReadOnlyCollection<InvitationRecord>> ListAsync(Guid organizationId, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var entities = await _dbContext.Invitations
            .AsNoTracking()
            .Where(invitation => invitation.OrganizationId == organizationId && invitation.ExpiresAtUtc > now)
            .OrderBy(invitation => invitation.ExpiresAtUtc)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.Select(MapToRecord).ToList();
    }

    public async Task<InvitationRecord?> FindAsync(Guid code, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.Invitations
            .AsNoTracking()
            .FirstOrDefaultAsync(invitation => invitation.Code == code, cancellationToken)
            .ConfigureAwait(false);

        if (entity is null)
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

    private async Task RemoveInternalAsync(Guid code, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.Invitations
            .FirstOrDefaultAsync(invitation => invitation.Code == code, cancellationToken)
            .ConfigureAwait(false);

        if (entity is null)
        {
            return;
        }

        _dbContext.Invitations.Remove(entity);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static InvitationRecord MapToRecord(OrganizationInvitation entity) => new()
    {
        Code = entity.Code,
        OrganizationId = entity.OrganizationId,
        OrganizationSlug = entity.OrganizationSlug,
        OrganizationName = entity.OrganizationName,
        Email = entity.Email,
        RoleIds = entity.RoleIds ?? Array.Empty<Guid>(),
        CreatedBy = entity.CreatedBy,
        CreatedAtUtc = entity.CreatedAtUtc,
        ExpiresAtUtc = entity.ExpiresAtUtc
    };

    private static OrganizationInvitation MapToEntity(InvitationRecord record) => new()
    {
        Code = record.Code,
        OrganizationId = record.OrganizationId,
        OrganizationSlug = record.OrganizationSlug,
        OrganizationName = record.OrganizationName,
        Email = record.Email,
        RoleIds = record.RoleIds ?? Array.Empty<Guid>(),
        CreatedBy = record.CreatedBy,
        CreatedAtUtc = record.CreatedAtUtc,
        ExpiresAtUtc = record.ExpiresAtUtc
    };
}

