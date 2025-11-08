using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Identity.Base.Abstractions.Pagination;
using Identity.Base.Extensions;
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

    public async Task<PagedResult<OrganizationInvitationRecord>> ListAsync(
        Guid organizationId,
        PageRequest pageRequest,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pageRequest);

        var normalized = pageRequest.WithDefaults();
        var now = DateTimeOffset.UtcNow;

        var query = _dbContext.Set<OrganizationInvitationEntity>()
            .AsNoTracking()
            .Where(invitation =>
                invitation.OrganizationId == organizationId &&
                invitation.ExpiresAtUtc > now &&
                invitation.UsedAtUtc == null);

        if (!string.IsNullOrWhiteSpace(normalized.Search))
        {
            if (_dbContext.Database.ProviderName?.Contains("InMemory", StringComparison.OrdinalIgnoreCase) == true)
            {
                var lower = normalized.Search.ToLowerInvariant();
                query = query.Where(invitation =>
                    invitation.Email.ToLower().Contains(lower) ||
                    invitation.OrganizationName.ToLower().Contains(lower));
            }
            else
            {
                var pattern = SearchPatternHelper.CreateSearchPattern(normalized.Search).ToUpperInvariant();
                query = query.Where(invitation =>
                    EF.Functions.Like(invitation.Email.ToUpper(), pattern) ||
                    EF.Functions.Like(invitation.OrganizationName.ToUpper(), pattern));
            }
        }

        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        if (total == 0)
        {
            return PagedResult<OrganizationInvitationRecord>.Empty(normalized.Page, normalized.PageSize);
        }

        var ordered = ApplyInvitationSorting(query, normalized);
        var skip = normalized.GetSkip();

        var entities = await ordered
            .Skip(skip)
            .Take(normalized.PageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var items = entities.Select(MapToRecord).ToList();
        return new PagedResult<OrganizationInvitationRecord>(normalized.Page, normalized.PageSize, total, items);
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

    private static IOrderedQueryable<OrganizationInvitationEntity> ApplyInvitationSorting(
        IQueryable<OrganizationInvitationEntity> source,
        PageRequest request)
    {
        IOrderedQueryable<OrganizationInvitationEntity>? ordered = null;

        if (request.Sorts.Count > 0)
        {
            foreach (var sort in request.Sorts)
            {
                var key = sort.Field.ToLowerInvariant();
                ordered = key switch
                {
                    "email" => ApplyInvitationOrder(source, ordered, invitation => invitation.Email, sort.Direction),
                    "createdat" => ApplyInvitationOrder(source, ordered, invitation => invitation.CreatedAtUtc, sort.Direction),
                    "expiresat" => ApplyInvitationOrder(source, ordered, invitation => invitation.ExpiresAtUtc, sort.Direction),
                    _ => ordered
                };

                if (ordered is not null)
                {
                    source = ordered;
                }
            }
        }

        ordered ??= source.OrderBy(invitation => invitation.ExpiresAtUtc)
            .ThenBy(invitation => invitation.CreatedAtUtc);

        return ordered.ThenBy(invitation => invitation.Code);
    }

    private static IOrderedQueryable<OrganizationInvitationEntity> ApplyInvitationOrder<T>(
        IQueryable<OrganizationInvitationEntity> source,
        IOrderedQueryable<OrganizationInvitationEntity>? ordered,
        System.Linq.Expressions.Expression<Func<OrganizationInvitationEntity, T>> keySelector,
        SortDirection direction)
    {
        if (ordered is null)
        {
            return direction == SortDirection.Descending
                ? source.OrderByDescending(keySelector)
                : source.OrderBy(keySelector);
        }

        return direction == SortDirection.Descending
            ? ordered.ThenByDescending(keySelector)
            : ordered.ThenBy(keySelector);
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
