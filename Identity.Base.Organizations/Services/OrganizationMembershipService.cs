using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Identity.Base.Abstractions.Pagination;
using Identity.Base.Data;
using Identity.Base.Extensions;
using Identity.Base.Organizations.Abstractions;
using Identity.Base.Organizations.Data;
using Identity.Base.Organizations.Domain;
using Identity.Base.Organizations.Lifecycle;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Identity.Base.Lifecycle;

namespace Identity.Base.Organizations.Services;

public sealed class OrganizationMembershipService : IOrganizationMembershipService
{
    private readonly OrganizationDbContext _dbContext;
    private readonly AppDbContext _appDbContext;
    private readonly ILogger<OrganizationMembershipService>? _logger;
    private readonly IOrganizationLifecycleHookDispatcher _lifecycleDispatcher;

    public OrganizationMembershipService(
        OrganizationDbContext dbContext,
        AppDbContext appDbContext,
        ILogger<OrganizationMembershipService>? logger = null,
        IOrganizationLifecycleHookDispatcher? lifecycleDispatcher = null)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _appDbContext = appDbContext ?? throw new ArgumentNullException(nameof(appDbContext));
        _logger = logger;
        _lifecycleDispatcher = lifecycleDispatcher ?? NullOrganizationLifecycleDispatcher.Instance;
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
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        var roleIds = NormalizeRoleIds(request.RoleIds);
        var lifecycleContext = new OrganizationLifecycleContext(
            OrganizationLifecycleEvent.MemberAdded,
            organization.Id,
            organization.Slug,
            organization.DisplayName,
            TargetUserId: request.UserId,
            Organization: organization,
            Items: new Dictionary<string, object?>
            {
                ["RoleIds"] = roleIds.ToArray()
            });

        await _lifecycleDispatcher.EnsureCanAddMemberAsync(lifecycleContext, cancellationToken).ConfigureAwait(false);

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

        await _lifecycleDispatcher.NotifyMemberAddedAsync(lifecycleContext, cancellationToken).ConfigureAwait(false);

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

        if (tenantId is Guid tenantFilter)
        {
            query = query.Where(membership => membership.TenantId == tenantFilter);
        }

        return await query
            .OrderBy(membership => membership.Organization!.DisplayName ?? membership.Organization!.Slug ?? string.Empty)
            .ThenBy(membership => membership.OrganizationId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<PagedResult<UserOrganizationMembership>> GetMembershipsForUserAsync(Guid userId, Guid? tenantId, PageRequest pageRequest, bool includeArchived, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pageRequest);

        var normalized = pageRequest.WithDefaults();

        var query = _dbContext.OrganizationMemberships
            .AsNoTracking()
            .Include(membership => membership.Organization)
            .Include(membership => membership.RoleAssignments)
            .Where(membership => membership.UserId == userId);

        if (tenantId is Guid tenantFilter)
        {
            query = query.Where(membership => membership.TenantId == tenantFilter);
        }

        query = query.Where(membership => membership.Organization != null);

        if (!includeArchived)
        {
            query = query.Where(membership => membership.Organization!.Status != OrganizationStatus.Archived);
        }

        if (!string.IsNullOrWhiteSpace(normalized.Search))
        {
            if (_dbContext.Database.ProviderName?.Contains("InMemory", StringComparison.OrdinalIgnoreCase) == true)
            {
                var lower = normalized.Search.ToLowerInvariant();
                query = query.Where(membership =>
                    (membership.Organization!.DisplayName ?? string.Empty).ToLower().Contains(lower) ||
                    (membership.Organization!.Slug ?? string.Empty).ToLower().Contains(lower));
            }
            else
            {
                var pattern = SearchPatternHelper.CreateSearchPattern(normalized.Search).ToUpperInvariant();
                query = query.Where(membership =>
                    EF.Functions.Like((membership.Organization!.DisplayName ?? string.Empty).ToUpper(), pattern) ||
                    EF.Functions.Like((membership.Organization!.Slug ?? string.Empty).ToUpper(), pattern));
            }
        }

        var totalCount = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        if (totalCount == 0)
        {
            return PagedResult<UserOrganizationMembership>.Empty(normalized.Page, normalized.PageSize);
        }

        var orderedQuery = ApplyUserOrganizationSorting(query, normalized);

        var skip = normalized.GetSkip();

        var items = await orderedQuery
            .Skip(skip)
            .Take(normalized.PageSize)
            .Select(membership => new UserOrganizationMembership(
                membership.OrganizationId,
                membership.TenantId,
                membership.Organization!.Slug,
                membership.Organization.DisplayName,
                membership.Organization.Status,
                membership.RoleAssignments.Select(assignment => assignment.RoleId).ToList(),
                membership.CreatedAtUtc,
                membership.UpdatedAtUtc))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new PagedResult<UserOrganizationMembership>(normalized.Page, normalized.PageSize, totalCount, items);
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

        if (request.RoleId.HasValue)
        {
            var roleId = request.RoleId.Value;
            membershipQuery = membershipQuery.Where(membership =>
                membership.RoleAssignments.Any(assignment => assignment.RoleId == roleId));
        }

        List<Guid>? userFilter = null;
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var usersQuery = _appDbContext.Users.AsNoTracking();

            if (_appDbContext.Database.ProviderName?.Contains("InMemory", StringComparison.OrdinalIgnoreCase) == true)
            {
                var lower = request.Search.Trim().ToLowerInvariant();
                usersQuery = usersQuery.Where(user =>
                    (user.Email ?? string.Empty).ToLower().Contains(lower) ||
                    (user.DisplayName ?? string.Empty).ToLower().Contains(lower) ||
                    (user.UserName ?? string.Empty).ToLower().Contains(lower));
            }
            else
            {
                var pattern = CreateSearchPattern(request.Search).ToUpperInvariant();
                usersQuery = usersQuery.Where(user =>
                    EF.Functions.Like((user.Email ?? string.Empty).ToUpper(), pattern) ||
                    EF.Functions.Like((user.DisplayName ?? string.Empty).ToUpper(), pattern) ||
                    EF.Functions.Like((user.UserName ?? string.Empty).ToUpper(), pattern));
            }

            userFilter = await usersQuery
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
                .OrderBy(membership => membership.CreatedAtUtc)
                .ThenBy(membership => membership.UserId),
            _ => membershipWithRoles
                .OrderByDescending(membership => membership.CreatedAtUtc)
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

    private static IOrderedQueryable<OrganizationMembership> ApplyUserOrganizationSorting(IQueryable<OrganizationMembership> source, PageRequest request)
    {
        var ordered = default(IOrderedQueryable<OrganizationMembership>);

        if (request.Sorts.Count > 0)
        {
            foreach (var sort in request.Sorts)
            {
                var key = sort.Field.ToLowerInvariant();
                ordered = key switch
                {
                    "displayname" => ApplyOrder(source, ordered, membership => membership.Organization!.DisplayName ?? membership.Organization!.Slug ?? string.Empty, sort.Direction),
                    "slug" => ApplyOrder(source, ordered, membership => membership.Organization!.Slug ?? string.Empty, sort.Direction),
                    "createdat" => ApplyOrder(source, ordered, membership => membership.CreatedAtUtc, sort.Direction),
                    _ => ordered
                };
                source = ordered ?? source;
            }
        }

        ordered = (ordered is null
            ? source
                .OrderBy(membership => membership.Organization!.DisplayName ?? membership.Organization!.Slug ?? string.Empty)
            : ordered
                .ThenBy(membership => membership.Organization!.DisplayName ?? membership.Organization!.Slug ?? string.Empty))
            .ThenBy(membership => membership.OrganizationId);

        return ordered;
    }

    private static IOrderedQueryable<OrganizationMembership> ApplyOrder<T>(
        IQueryable<OrganizationMembership> source,
        IOrderedQueryable<OrganizationMembership>? ordered,
        Expression<Func<OrganizationMembership, T>> keySelector,
        SortDirection direction)
    {
        if (ordered is null)
        {
            return direction == SortDirection.Ascending
                ? source.OrderBy(keySelector)
                : source.OrderByDescending(keySelector);
        }

        return direction == SortDirection.Ascending
            ? ordered.ThenBy(keySelector)
            : ordered.ThenByDescending(keySelector);
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
        OrganizationLifecycleContext? updateContext = null;

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

                var requestedRoleIds = roleIds.ToArray();
                updateContext ??= new OrganizationLifecycleContext(
                    OrganizationLifecycleEvent.MembershipUpdated,
                    organization.Id,
                    organization.Slug,
                    organization.DisplayName,
                    TargetUserId: membership.UserId,
                    Items: new Dictionary<string, object?>
                    {
                        ["RoleIds"] = requestedRoleIds
                    });

                await _lifecycleDispatcher.EnsureCanUpdateMembershipAsync(updateContext, cancellationToken).ConfigureAwait(false);

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

        if (updateContext is not null)
        {
            await _lifecycleDispatcher.NotifyMembershipUpdatedAsync(updateContext, cancellationToken).ConfigureAwait(false);
        }

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
            .Include(entity => entity.Organization)
            .FirstOrDefaultAsync(entity => entity.OrganizationId == organizationId && entity.UserId == userId, cancellationToken)
            .ConfigureAwait(false);

        if (membership is null)
        {
            return;
        }

        var lifecycleContext = new OrganizationLifecycleContext(
            OrganizationLifecycleEvent.MembershipRevoked,
            membership.OrganizationId,
            membership.Organization?.Slug,
            membership.Organization?.DisplayName,
            TargetUserId: membership.UserId);

        await _lifecycleDispatcher.EnsureCanRevokeMembershipAsync(lifecycleContext, cancellationToken).ConfigureAwait(false);

        _dbContext.OrganizationMemberships.Remove(membership);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger?.LogInformation("Removed user {UserId} from organization {OrganizationId}", userId, organizationId);

        await _lifecycleDispatcher.NotifyMembershipRevokedAsync(lifecycleContext, cancellationToken).ConfigureAwait(false);
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
