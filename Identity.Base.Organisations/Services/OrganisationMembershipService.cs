using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Identity.Base.Data;
using Identity.Base.Organisations.Abstractions;
using Identity.Base.Organisations.Data;
using Identity.Base.Organisations.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Identity.Base.Organisations.Services;

public sealed class OrganisationMembershipService : IOrganisationMembershipService
{
    private readonly OrganisationDbContext _dbContext;
    private readonly AppDbContext _appDbContext;
    private readonly ILogger<OrganisationMembershipService>? _logger;

    public OrganisationMembershipService(
        OrganisationDbContext dbContext,
        AppDbContext appDbContext,
        ILogger<OrganisationMembershipService>? logger = null)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _appDbContext = appDbContext ?? throw new ArgumentNullException(nameof(appDbContext));
        _logger = logger;
    }

    public async Task<OrganisationMembership> AddMemberAsync(OrganisationMembershipRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.OrganisationId == Guid.Empty)
        {
            throw new ArgumentException("Organisation identifier is required.", nameof(request));
        }

        if (request.UserId == Guid.Empty)
        {
            throw new ArgumentException("User identifier is required.", nameof(request));
        }

        var organisation = await _dbContext.Organisations
            .AsNoTracking()
            .FirstOrDefaultAsync(entity => entity.Id == request.OrganisationId, cancellationToken)
            .ConfigureAwait(false);

        if (organisation is null)
        {
            throw new KeyNotFoundException($"Organisation {request.OrganisationId} was not found.");
        }

        if (organisation.TenantId.HasValue && request.TenantId.HasValue && organisation.TenantId != request.TenantId)
        {
            throw new InvalidOperationException("Organisation and membership tenants do not match.");
        }

        var membershipExists = await _dbContext.OrganisationMemberships
            .AnyAsync(entity => entity.OrganisationId == request.OrganisationId && entity.UserId == request.UserId, cancellationToken)
            .ConfigureAwait(false);

        if (membershipExists)
        {
            throw new InvalidOperationException("The user is already a member of this organisation.");
        }

        var membership = new OrganisationMembership
        {
            OrganisationId = request.OrganisationId,
            UserId = request.UserId,
            TenantId = organisation.TenantId,
            IsPrimary = request.IsPrimary,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        if (request.IsPrimary)
        {
            await ClearPrimaryMembershipAsync(request.UserId, organisation.TenantId, cancellationToken).ConfigureAwait(false);
        }

        var roleIds = NormalizeRoleIds(request.RoleIds);
        if (roleIds.Count > 0)
        {
            var roles = await _dbContext.OrganisationRoles
                .Where(role => roleIds.Contains(role.Id))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            if (roles.Count != roleIds.Count)
            {
                throw new KeyNotFoundException("One or more roles could not be found.");
            }

            ValidateRolesForOrganisation(request.OrganisationId, organisation.TenantId, roles);

            foreach (var role in roles)
            {
                membership.RoleAssignments.Add(new OrganisationRoleAssignment
                {
                    OrganisationId = membership.OrganisationId,
                    UserId = membership.UserId,
                    RoleId = role.Id,
                    TenantId = organisation.TenantId,
                    CreatedAtUtc = DateTimeOffset.UtcNow
                });
            }
        }

        _dbContext.OrganisationMemberships.Add(membership);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await _dbContext.Entry(membership)
            .Collection(m => m.RoleAssignments)
            .LoadAsync(cancellationToken)
            .ConfigureAwait(false);

        _logger?.LogInformation(
            "Added user {UserId} to organisation {OrganisationId} with {RoleCount} roles",
            membership.UserId,
            membership.OrganisationId,
            membership.RoleAssignments.Count);

        return membership;
    }

    public async Task<OrganisationMembership?> GetMembershipAsync(Guid organisationId, Guid userId, CancellationToken cancellationToken = default)
    {
        if (organisationId == Guid.Empty || userId == Guid.Empty)
        {
            return null;
        }

        return await _dbContext.OrganisationMemberships
            .Include(membership => membership.Organisation)
            .Include(membership => membership.RoleAssignments)
                .ThenInclude(assignment => assignment.Role)
            .AsNoTracking()
            .FirstOrDefaultAsync(membership => membership.OrganisationId == organisationId && membership.UserId == userId, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<OrganisationMembership>> GetMembershipsForUserAsync(Guid userId, Guid? tenantId, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            return Array.Empty<OrganisationMembership>();
        }

        var query = _dbContext.OrganisationMemberships
            .Include(membership => membership.Organisation)
            .Include(membership => membership.RoleAssignments)
                .ThenInclude(assignment => assignment.Role)
            .AsNoTracking()
            .Where(membership => membership.UserId == userId);

        if (tenantId.HasValue)
        {
            query = query.Where(membership => membership.TenantId == tenantId.Value);
        }

        return await query
            .OrderByDescending(membership => membership.IsPrimary)
            .ThenBy(membership => membership.OrganisationId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<OrganisationMemberListResult> GetMembersAsync(OrganisationMemberListRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.OrganisationId == Guid.Empty)
        {
            throw new ArgumentException("Organisation identifier is required.", nameof(request));
        }

        const int defaultPageSize = 25;
        const int maxPageSize = 200;

        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize < 1 ? defaultPageSize : Math.Min(request.PageSize, maxPageSize);

        var membershipQuery = _dbContext.OrganisationMemberships
            .AsNoTracking()
            .Where(membership => membership.OrganisationId == request.OrganisationId);

        if (request.IsPrimary.HasValue)
        {
            membershipQuery = membershipQuery.Where(membership => membership.IsPrimary == request.IsPrimary.Value);
        }

        if (request.RoleId.HasValue)
        {
            var roleId = request.RoleId.Value;
            membershipQuery = membershipQuery.Where(membership =>
                membership.RoleAssignments.Any(assignment => assignment.RoleId == roleId));
        }

        List<Guid>? userFilter = null;
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var pattern = CreateSearchPattern(request.Search);
            userFilter = await _appDbContext.Users
                .AsNoTracking()
                .Where(user =>
                    EF.Functions.ILike(user.Email ?? string.Empty, pattern) ||
                    EF.Functions.ILike(user.DisplayName ?? string.Empty, pattern) ||
                    EF.Functions.ILike(user.UserName ?? string.Empty, pattern))
                .Select(user => user.Id)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            if (userFilter.Count == 0)
            {
                return OrganisationMemberListResult.Empty(page, pageSize);
            }

            membershipQuery = membershipQuery.Where(membership => userFilter.Contains(membership.UserId));
        }

        var totalCount = await membershipQuery.CountAsync(cancellationToken).ConfigureAwait(false);
        if (totalCount == 0)
        {
            return OrganisationMemberListResult.Empty(page, pageSize);
        }

        var maxPage = (int)Math.Ceiling(totalCount / (double)pageSize);
        if (page > maxPage)
        {
            page = maxPage;
        }

        var skip = (page - 1) * pageSize;

        var membershipWithRoles = membershipQuery.Include(membership => membership.RoleAssignments);

        var orderedQuery = request.Sort switch
        {
            OrganisationMemberSort.CreatedAtAscending => membershipWithRoles
                .OrderBy(membership => membership.IsPrimary)
                .ThenBy(membership => membership.CreatedAtUtc)
                .ThenBy(membership => membership.UserId),
            _ => membershipWithRoles
                .OrderByDescending(membership => membership.IsPrimary)
                .ThenByDescending(membership => membership.CreatedAtUtc)
                .ThenBy(membership => membership.UserId)
        };

        var memberships = await orderedQuery
            .Skip(skip)
            .Take(pageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var pageUserIds = memberships
            .Select(membership => membership.UserId)
            .Distinct()
            .ToList();

        var userLookup = await _appDbContext.Users
            .AsNoTracking()
            .Where(user => pageUserIds.Contains(user.Id))
            .Select(user => new UserProjection(user.Id, user.Email, user.DisplayName))
            .ToDictionaryAsync(user => user.Id, cancellationToken)
            .ConfigureAwait(false);

        var members = memberships
            .Select(membership =>
            {
                userLookup.TryGetValue(membership.UserId, out var user);
                return new OrganisationMemberListItem
                {
                    OrganisationId = membership.OrganisationId,
                    UserId = membership.UserId,
                    TenantId = membership.TenantId,
                    IsPrimary = membership.IsPrimary,
                    RoleIds = membership.RoleAssignments.Select(assignment => assignment.RoleId).ToArray(),
                    CreatedAtUtc = membership.CreatedAtUtc,
                    UpdatedAtUtc = membership.UpdatedAtUtc,
                    Email = user?.Email,
                    DisplayName = user?.DisplayName
                };
            })
            .ToList();

        _logger?.LogDebug(
            "Loaded {MemberCount} organisation members for organisation {OrganisationId} on page {Page} of {TotalPages}",
            members.Count,
            request.OrganisationId,
            page,
            maxPage);

        return new OrganisationMemberListResult(page, pageSize, totalCount, members);
    }

    private static string CreateSearchPattern(string rawSearch)
    {
        var trimmed = rawSearch.Trim();
        if (trimmed.Length == 0)
        {
            return "%";
        }

        var escaped = trimmed
            .Replace(@"\", @"\\", StringComparison.Ordinal)
            .Replace("%", @"\%", StringComparison.Ordinal)
            .Replace("_", @"\_", StringComparison.Ordinal);

        return $"%{escaped}%";
    }

    private sealed record UserProjection(Guid Id, string? Email, string? DisplayName);

    public async Task<OrganisationMembership> UpdateMembershipAsync(OrganisationMembershipUpdateRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.OrganisationId == Guid.Empty)
        {
            throw new ArgumentException("Organisation identifier is required.", nameof(request));
        }

        if (request.UserId == Guid.Empty)
        {
            throw new ArgumentException("User identifier is required.", nameof(request));
        }

        var membership = await _dbContext.OrganisationMemberships
            .Include(entity => entity.RoleAssignments)
            .FirstOrDefaultAsync(entity => entity.OrganisationId == request.OrganisationId && entity.UserId == request.UserId, cancellationToken)
            .ConfigureAwait(false);

        if (membership is null)
        {
            throw new KeyNotFoundException("Membership not found.");
        }

        var organisation = await _dbContext.Organisations
            .AsNoTracking()
            .FirstOrDefaultAsync(entity => entity.Id == request.OrganisationId, cancellationToken)
            .ConfigureAwait(false);

        if (organisation is null)
        {
            throw new KeyNotFoundException($"Organisation {request.OrganisationId} was not found.");
        }

        var changed = false;

        if (request.IsPrimary.HasValue)
        {
            if (request.IsPrimary.Value && !membership.IsPrimary)
            {
                await ClearPrimaryMembershipAsync(request.UserId, organisation.TenantId, cancellationToken).ConfigureAwait(false);
                membership.IsPrimary = true;
                changed = true;
            }
            else if (!request.IsPrimary.Value && membership.IsPrimary)
            {
                membership.IsPrimary = false;
                changed = true;
            }
        }

        if (request.RoleIds is not null)
        {
            var roleIds = NormalizeRoleIds(request.RoleIds);
            var existingAssignments = membership.RoleAssignments.ToDictionary(assignment => assignment.RoleId);

            if (!roleIds.SetEquals(existingAssignments.Keys))
            {
                var roles = await _dbContext.OrganisationRoles
                    .Where(role => roleIds.Contains(role.Id))
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                if (roles.Count != roleIds.Count)
                {
                    throw new KeyNotFoundException("One or more roles could not be found.");
                }

                ValidateRolesForOrganisation(request.OrganisationId, organisation.TenantId, roles);

                foreach (var assignment in existingAssignments.Values)
                {
                    if (!roleIds.Contains(assignment.RoleId))
                    {
                        _dbContext.OrganisationRoleAssignments.Remove(assignment);
                    }
                }

                foreach (var role in roles)
                {
                    if (!existingAssignments.ContainsKey(role.Id))
                    {
                        membership.RoleAssignments.Add(new OrganisationRoleAssignment
                        {
                            OrganisationId = membership.OrganisationId,
                            UserId = membership.UserId,
                            RoleId = role.Id,
                            TenantId = organisation.TenantId,
                            CreatedAtUtc = DateTimeOffset.UtcNow
                        });
                    }
                }

                changed = true;
            }
        }

        if (!changed)
        {
            return membership;
        }

        membership.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger?.LogInformation(
            "Updated membership for user {UserId} in organisation {OrganisationId}",
            membership.UserId,
            membership.OrganisationId);

        return membership;
    }

    public async Task RemoveMemberAsync(Guid organisationId, Guid userId, CancellationToken cancellationToken = default)
    {
        if (organisationId == Guid.Empty)
        {
            throw new ArgumentException("Organisation identifier is required.", nameof(organisationId));
        }

        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User identifier is required.", nameof(userId));
        }

        var membership = await _dbContext.OrganisationMemberships
            .FirstOrDefaultAsync(entity => entity.OrganisationId == organisationId && entity.UserId == userId, cancellationToken)
            .ConfigureAwait(false);

        if (membership is null)
        {
            return;
        }

        _dbContext.OrganisationMemberships.Remove(membership);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger?.LogInformation("Removed user {UserId} from organisation {OrganisationId}", userId, organisationId);
    }

    private async Task ClearPrimaryMembershipAsync(Guid userId, Guid? tenantId, CancellationToken cancellationToken)
    {
        var query = _dbContext.OrganisationMemberships
            .Where(membership => membership.UserId == userId && membership.IsPrimary);

        if (tenantId.HasValue)
        {
            query = query.Where(membership => membership.TenantId == tenantId.Value);
        }

        var memberships = await query.ToListAsync(cancellationToken).ConfigureAwait(false);
        if (memberships.Count == 0)
        {
            return;
        }

        foreach (var membership in memberships)
        {
            membership.IsPrimary = false;
            membership.UpdatedAtUtc = DateTimeOffset.UtcNow;
        }
    }

    private static HashSet<Guid> NormalizeRoleIds(IEnumerable<Guid> roleIds)
    {
        var result = new HashSet<Guid>();
        foreach (var roleId in roleIds ?? Array.Empty<Guid>())
        {
            if (roleId == Guid.Empty)
            {
                continue;
            }

            result.Add(roleId);
        }

        return result;
    }

    private static void ValidateRolesForOrganisation(Guid organisationId, Guid? tenantId, IReadOnlyCollection<OrganisationRole> roles)
    {
        foreach (var role in roles)
        {
            if (role.OrganisationId.HasValue && role.OrganisationId != organisationId)
            {
                throw new InvalidOperationException($"Role {role.Id} does not belong to organisation {organisationId}.");
            }

            if (tenantId.HasValue && role.TenantId.HasValue && role.TenantId != tenantId)
            {
                throw new InvalidOperationException($"Role {role.Id} does not belong to the tenant for this organisation.");
            }
        }
    }
}
