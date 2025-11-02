using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Identity.Base.Data;
using Identity.Base.Organizations.Abstractions;
using Identity.Base.Organizations.Data;
using Identity.Base.Organizations.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Identity.Base.Organizations.Services;

public sealed class OrganizationMembershipService : IOrganizationMembershipService
{
    private readonly OrganizationDbContext _dbContext;
    private readonly AppDbContext _appDbContext;
    private readonly ILogger<OrganizationMembershipService>? _logger;

    public OrganizationMembershipService(
        OrganizationDbContext dbContext,
        AppDbContext appDbContext,
        ILogger<OrganizationMembershipService>? logger = null)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _appDbContext = appDbContext ?? throw new ArgumentNullException(nameof(appDbContext));
        _logger = logger;
    }

    public async Task<OrganizationMembership> AddMemberAsync(OrganizationMembershipRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.OrganizationId == Guid.Empty)
        {
            throw new ArgumentException("Organization identifier is required.", nameof(request));
        }

        if (request.UserId == Guid.Empty)
        {
            throw new ArgumentException("User identifier is required.", nameof(request));
        }

        var organization = await _dbContext.Organizations
            .AsNoTracking()
            .FirstOrDefaultAsync(entity => entity.Id == request.OrganizationId, cancellationToken)
            .ConfigureAwait(false);

        if (organization is null)
        {
            throw new KeyNotFoundException($"Organization {request.OrganizationId} was not found.");
        }

        if (organization.TenantId.HasValue && request.TenantId.HasValue && organization.TenantId != request.TenantId)
        {
            throw new InvalidOperationException("Organization and membership tenants do not match.");
        }

        var membershipExists = await _dbContext.OrganizationMemberships
            .AnyAsync(entity => entity.OrganizationId == request.OrganizationId && entity.UserId == request.UserId, cancellationToken)
            .ConfigureAwait(false);

        if (membershipExists)
        {
            throw new InvalidOperationException("The user is already a member of this organization.");
        }

        var membership = new OrganizationMembership
        {
            OrganizationId = request.OrganizationId,
            UserId = request.UserId,
            TenantId = organization.TenantId,
            IsPrimary = request.IsPrimary,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        if (request.IsPrimary)
        {
            await ClearPrimaryMembershipAsync(request.UserId, organization.TenantId, cancellationToken).ConfigureAwait(false);
        }

        var roleIds = NormalizeRoleIds(request.RoleIds);
        if (roleIds.Count > 0)
        {
            var roles = await _dbContext.OrganizationRoles
                .Where(role => roleIds.Contains(role.Id))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            if (roles.Count != roleIds.Count)
            {
                throw new KeyNotFoundException("One or more roles could not be found.");
            }

            ValidateRolesForOrganization(request.OrganizationId, organization.TenantId, roles);

            foreach (var role in roles)
            {
                membership.RoleAssignments.Add(new OrganizationRoleAssignment
                {
                    OrganizationId = membership.OrganizationId,
                    UserId = membership.UserId,
                    RoleId = role.Id,
                    TenantId = organization.TenantId,
                    CreatedAtUtc = DateTimeOffset.UtcNow
                });
            }
        }

        _dbContext.OrganizationMemberships.Add(membership);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await _dbContext.Entry(membership)
            .Collection(m => m.RoleAssignments)
            .LoadAsync(cancellationToken)
            .ConfigureAwait(false);

        _logger?.LogInformation(
            "Added user {UserId} to organization {OrganizationId} with {RoleCount} roles",
            membership.UserId,
            membership.OrganizationId,
            membership.RoleAssignments.Count);

        return membership;
    }

    public async Task<OrganizationMembership?> GetMembershipAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken = default)
    {
        if (organizationId == Guid.Empty || userId == Guid.Empty)
        {
            return null;
        }

        return await _dbContext.OrganizationMemberships
            .Include(membership => membership.Organization)
            .Include(membership => membership.RoleAssignments)
                .ThenInclude(assignment => assignment.Role)
            .AsNoTracking()
            .FirstOrDefaultAsync(membership => membership.OrganizationId == organizationId && membership.UserId == userId, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<OrganizationMembership>> GetMembershipsForUserAsync(Guid userId, Guid? tenantId, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            return Array.Empty<OrganizationMembership>();
        }

        var query = _dbContext.OrganizationMemberships
            .Include(membership => membership.Organization)
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
            .ThenBy(membership => membership.OrganizationId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<OrganizationMemberListResult> GetMembersAsync(OrganizationMemberListRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.OrganizationId == Guid.Empty)
        {
            throw new ArgumentException("Organization identifier is required.", nameof(request));
        }

        const int defaultPageSize = 25;
        const int maxPageSize = 200;

        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize < 1 ? defaultPageSize : Math.Min(request.PageSize, maxPageSize);

        var membershipQuery = _dbContext.OrganizationMemberships
            .AsNoTracking()
            .Where(membership => membership.OrganizationId == request.OrganizationId);

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
                return OrganizationMemberListResult.Empty(page, pageSize);
            }

            membershipQuery = membershipQuery.Where(membership => userFilter.Contains(membership.UserId));
        }

        var totalCount = await membershipQuery.CountAsync(cancellationToken).ConfigureAwait(false);
        if (totalCount == 0)
        {
            return OrganizationMemberListResult.Empty(page, pageSize);
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
            OrganizationMemberSort.CreatedAtAscending => membershipWithRoles
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
                return new OrganizationMemberListItem
                {
                    OrganizationId = membership.OrganizationId,
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
            "Loaded {MemberCount} organization members for organization {OrganizationId} on page {Page} of {TotalPages}",
            members.Count,
            request.OrganizationId,
            page,
            maxPage);

        return new OrganizationMemberListResult(page, pageSize, totalCount, members);
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

    public async Task<OrganizationMembership> UpdateMembershipAsync(OrganizationMembershipUpdateRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.OrganizationId == Guid.Empty)
        {
            throw new ArgumentException("Organization identifier is required.", nameof(request));
        }

        if (request.UserId == Guid.Empty)
        {
            throw new ArgumentException("User identifier is required.", nameof(request));
        }

        var membership = await _dbContext.OrganizationMemberships
            .Include(entity => entity.RoleAssignments)
            .FirstOrDefaultAsync(entity => entity.OrganizationId == request.OrganizationId && entity.UserId == request.UserId, cancellationToken)
            .ConfigureAwait(false);

        if (membership is null)
        {
            throw new KeyNotFoundException("Membership not found.");
        }

        var organization = await _dbContext.Organizations
            .AsNoTracking()
            .FirstOrDefaultAsync(entity => entity.Id == request.OrganizationId, cancellationToken)
            .ConfigureAwait(false);

        if (organization is null)
        {
            throw new KeyNotFoundException($"Organization {request.OrganizationId} was not found.");
        }

        var changed = false;

        if (request.IsPrimary.HasValue)
        {
            if (request.IsPrimary.Value && !membership.IsPrimary)
            {
                await ClearPrimaryMembershipAsync(request.UserId, organization.TenantId, cancellationToken).ConfigureAwait(false);
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
                var roles = await _dbContext.OrganizationRoles
                    .Where(role => roleIds.Contains(role.Id))
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                if (roles.Count != roleIds.Count)
                {
                    throw new KeyNotFoundException("One or more roles could not be found.");
                }

                ValidateRolesForOrganization(request.OrganizationId, organization.TenantId, roles);

                foreach (var assignment in existingAssignments.Values)
                {
                    if (!roleIds.Contains(assignment.RoleId))
                    {
                        _dbContext.OrganizationRoleAssignments.Remove(assignment);
                    }
                }

                foreach (var role in roles)
                {
                    if (!existingAssignments.ContainsKey(role.Id))
                    {
                        membership.RoleAssignments.Add(new OrganizationRoleAssignment
                        {
                            OrganizationId = membership.OrganizationId,
                            UserId = membership.UserId,
                            RoleId = role.Id,
                            TenantId = organization.TenantId,
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
            "Updated membership for user {UserId} in organization {OrganizationId}",
            membership.UserId,
            membership.OrganizationId);

        return membership;
    }

    public async Task RemoveMemberAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken = default)
    {
        if (organizationId == Guid.Empty)
        {
            throw new ArgumentException("Organization identifier is required.", nameof(organizationId));
        }

        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User identifier is required.", nameof(userId));
        }

        var membership = await _dbContext.OrganizationMemberships
            .FirstOrDefaultAsync(entity => entity.OrganizationId == organizationId && entity.UserId == userId, cancellationToken)
            .ConfigureAwait(false);

        if (membership is null)
        {
            return;
        }

        _dbContext.OrganizationMemberships.Remove(membership);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger?.LogInformation("Removed user {UserId} from organization {OrganizationId}", userId, organizationId);
    }

    private async Task ClearPrimaryMembershipAsync(Guid userId, Guid? tenantId, CancellationToken cancellationToken)
    {
        var query = _dbContext.OrganizationMemberships
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

    private static void ValidateRolesForOrganization(Guid organizationId, Guid? tenantId, IReadOnlyCollection<OrganizationRole> roles)
    {
        foreach (var role in roles)
        {
            if (role.OrganizationId.HasValue && role.OrganizationId != organizationId)
            {
                throw new InvalidOperationException($"Role {role.Id} does not belong to organization {organizationId}.");
            }

            if (tenantId.HasValue && role.TenantId.HasValue && role.TenantId != tenantId)
            {
                throw new InvalidOperationException($"Role {role.Id} does not belong to the tenant for this organization.");
            }
        }
    }
}
