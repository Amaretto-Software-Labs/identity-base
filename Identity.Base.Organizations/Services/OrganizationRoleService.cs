using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Identity.Base.Organizations.Abstractions;
using Identity.Base.Organizations.Data;
using Identity.Base.Organizations.Domain;
using Identity.Base.Organizations.Options;
using Identity.Base.Roles.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Identity.Base.Organizations.Services;

public sealed class OrganizationRoleService : IOrganizationRoleService
{
    private readonly OrganizationDbContext _dbContext;
    private readonly IRoleDbContext _roleDbContext;
    private readonly OrganizationRoleOptions _options;
    private readonly ILogger<OrganizationRoleService>? _logger;

    public OrganizationRoleService(
        OrganizationDbContext dbContext,
        IRoleDbContext roleDbContext,
        IOptions<OrganizationRoleOptions> options,
        ILogger<OrganizationRoleService>? logger = null)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _roleDbContext = roleDbContext ?? throw new ArgumentNullException(nameof(roleDbContext));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger;
    }

    public async Task<OrganizationRole> CreateAsync(OrganizationRoleCreateRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ArgumentException("Role name is required.", nameof(request));
        }

        var name = request.Name.Trim();
        if (name.Length > _options.NameMaxLength)
        {
            throw new ArgumentException($"Role name cannot exceed {_options.NameMaxLength} characters.", nameof(request));
        }

        if (request.Description is { Length: > 0 } description && description.Length > _options.DescriptionMaxLength)
        {
            throw new ArgumentException($"Role description cannot exceed {_options.DescriptionMaxLength} characters.", nameof(request));
        }

        var tenantId = request.TenantId;
        if (request.OrganizationId.HasValue)
        {
            var organization = await _dbContext.Organizations
                .AsNoTracking()
                .FirstOrDefaultAsync(org => org.Id == request.OrganizationId.Value, cancellationToken)
                .ConfigureAwait(false);

            if (organization is null)
            {
                throw new KeyNotFoundException($"Organization {request.OrganizationId.Value} was not found.");
            }

            if (tenantId.HasValue && organization.TenantId.HasValue && tenantId.Value != organization.TenantId.Value)
            {
                throw new InvalidOperationException("Organization and role tenants do not match.");
            }

            tenantId ??= organization.TenantId;
        }

        await EnsureRoleNameIsUniqueAsync(tenantId, request.OrganizationId, name, cancellationToken).ConfigureAwait(false);

        var role = new OrganizationRole
        {
            Id = Guid.NewGuid(),
            OrganizationId = request.OrganizationId,
            TenantId = tenantId,
            Name = name,
            Description = request.Description?.Trim(),
            IsSystemRole = request.IsSystemRole,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        _dbContext.OrganizationRoles.Add(role);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger?.LogInformation(
            "Created organization role {RoleId} (Name: {RoleName}) for organization {OrganizationId}",
            role.Id,
            role.Name,
            role.OrganizationId);

        return role;
    }

    public async Task<OrganizationRole?> GetByIdAsync(Guid roleId, CancellationToken cancellationToken = default)
    {
        if (roleId == Guid.Empty)
        {
            return null;
        }

        return await _dbContext.OrganizationRoles
            .AsNoTracking()
            .FirstOrDefaultAsync(role => role.Id == roleId, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<OrganizationRole>> ListAsync(Guid? tenantId, Guid? organizationId, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.OrganizationRoles.AsNoTracking().AsQueryable();

        if (tenantId.HasValue)
        {
            query = query.Where(role => role.TenantId == tenantId.Value || role.TenantId == null);
        }
        else
        {
            query = query.Where(role => role.TenantId == null);
        }

        if (organizationId.HasValue)
        {
            query = query.Where(role => role.OrganizationId == organizationId.Value || role.OrganizationId == null);
        }

        return await query
            .OrderBy(role => role.OrganizationId.HasValue ? 1 : 0)
            .ThenBy(role => role.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task DeleteAsync(Guid roleId, CancellationToken cancellationToken = default)
    {
        if (roleId == Guid.Empty)
        {
            throw new ArgumentException("Role identifier is required.", nameof(roleId));
        }

        var role = await _dbContext.OrganizationRoles
            .FirstOrDefaultAsync(entity => entity.Id == roleId, cancellationToken)
            .ConfigureAwait(false);

        if (role is null)
        {
            return;
        }

        if (role.IsSystemRole)
        {
            throw new InvalidOperationException("System roles cannot be deleted.");
        }

        _dbContext.OrganizationRoles.Remove(role);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger?.LogInformation("Deleted organization role {RoleId}", roleId);
    }

    public async Task<OrganizationRolePermissionSet> GetPermissionsAsync(Guid roleId, Guid organizationId, CancellationToken cancellationToken = default)
    {
        if (roleId == Guid.Empty)
        {
            throw new ArgumentException("Role identifier is required.", nameof(roleId));
        }

        if (organizationId == Guid.Empty)
        {
            throw new ArgumentException("Organization identifier is required.", nameof(organizationId));
        }

        var role = await _dbContext.OrganizationRoles
            .AsNoTracking()
            .FirstOrDefaultAsync(entity => entity.Id == roleId, cancellationToken)
            .ConfigureAwait(false);

        if (role is null)
        {
            throw new KeyNotFoundException($"Organization role {roleId} was not found.");
        }

        var organization = await _dbContext.Organizations
            .AsNoTracking()
            .FirstOrDefaultAsync(entity => entity.Id == organizationId, cancellationToken)
            .ConfigureAwait(false);

        if (organization is null)
        {
            throw new KeyNotFoundException($"Organization {organizationId} was not found.");
        }

        var effectiveIds = new HashSet<Guid>();
        var explicitIds = new HashSet<Guid>();

        var tenantId = organization.TenantId ?? role.TenantId;

        var permissionQuery = _dbContext.OrganizationRolePermissions
            .AsNoTracking()
            .Where(permission => permission.RoleId == roleId);

        if (tenantId.HasValue)
        {
            var tenant = tenantId.Value;
            permissionQuery = permissionQuery.Where(permission => permission.TenantId == null || permission.TenantId == tenant);
        }
        else
        {
            permissionQuery = permissionQuery.Where(permission => permission.TenantId == null);
        }

        var explicitPermissionIds = await permissionQuery
            .Where(permission => permission.OrganizationId == organizationId)
            .Select(permission => permission.PermissionId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var id in explicitPermissionIds)
        {
            explicitIds.Add(id);
            effectiveIds.Add(id);
        }

        if (!role.OrganizationId.HasValue || role.OrganizationId.Value != organizationId)
        {
            var inheritedIds = await permissionQuery
                .Where(permission => permission.OrganizationId == role.OrganizationId)
                .Select(permission => permission.PermissionId)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            foreach (var id in inheritedIds)
            {
                effectiveIds.Add(id);
            }
        }

        if (effectiveIds.Count == 0)
        {
            return new OrganizationRolePermissionSet(Array.Empty<string>(), Array.Empty<string>());
        }

        var lookup = await _roleDbContext.Permissions
            .AsNoTracking()
            .Where(permission => effectiveIds.Contains(permission.Id) || explicitIds.Contains(permission.Id))
            .Select(permission => new { permission.Id, permission.Name })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var nameById = new Dictionary<Guid, string>(lookup.Count);
        foreach (var permission in lookup)
        {
            if (!string.IsNullOrWhiteSpace(permission.Name))
            {
                nameById[permission.Id] = permission.Name;
            }
        }

        static List<string> MapNames(IEnumerable<Guid> ids, IDictionary<Guid, string> mapping, ILogger? logger, Guid roleId)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var id in ids)
            {
                if (mapping.TryGetValue(id, out var name) && !string.IsNullOrWhiteSpace(name))
                {
                    names.Add(name.Trim());
                }
                else
                {
                    logger?.LogWarning("Permission {PermissionId} referenced by organization role {RoleId} but not found in catalog.", id, roleId);
                }
            }

            return names.OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToList();
        }

        var effective = MapNames(effectiveIds, nameById, _logger, roleId);
        var explicitNames = MapNames(explicitIds, nameById, _logger, roleId);

        return new OrganizationRolePermissionSet(effective, explicitNames);
    }

    public async Task UpdatePermissionsAsync(Guid roleId, Guid organizationId, IEnumerable<string> permissions, CancellationToken cancellationToken = default)
    {
        if (roleId == Guid.Empty)
        {
            throw new ArgumentException("Role identifier is required.", nameof(roleId));
        }

        if (organizationId == Guid.Empty)
        {
            throw new ArgumentException("Organization identifier is required.", nameof(organizationId));
        }

        ArgumentNullException.ThrowIfNull(permissions);

        var organization = await _dbContext.Organizations
            .FirstOrDefaultAsync(entity => entity.Id == organizationId, cancellationToken)
            .ConfigureAwait(false);

        if (organization is null)
        {
            throw new KeyNotFoundException($"Organization {organizationId} was not found.");
        }

        var role = await _dbContext.OrganizationRoles
            .FirstOrDefaultAsync(entity => entity.Id == roleId, cancellationToken)
            .ConfigureAwait(false);

        if (role is null)
        {
            throw new KeyNotFoundException($"Organization role {roleId} was not found.");
        }

        if (role.OrganizationId.HasValue && role.OrganizationId.Value != organizationId)
        {
            throw new InvalidOperationException("Role does not belong to the specified organization scope.");
        }

        var desiredNames = permissions
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var desiredIds = new HashSet<Guid>();
        if (desiredNames.Count > 0)
        {
            var knownPermissions = await _roleDbContext.Permissions
                .Where(permission => desiredNames.Contains(permission.Name))
                .Select(permission => new { permission.Id, permission.Name })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            var nameLookup = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
            foreach (var permission in knownPermissions)
            {
                nameLookup[permission.Name] = permission.Id;
            }

            var missing = desiredNames.Where(name => !nameLookup.ContainsKey(name)).ToList();
            if (missing.Count > 0)
            {
                throw new KeyNotFoundException($"Unknown permissions: {string.Join(", ", missing)}");
            }

            foreach (var name in desiredNames)
            {
                desiredIds.Add(nameLookup[name]);
            }
        }

        var existingAssignments = await _dbContext.OrganizationRolePermissions
            .Where(permission => permission.RoleId == roleId && permission.OrganizationId == organizationId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var removed = 0;
        foreach (var assignment in existingAssignments)
        {
            if (!desiredIds.Contains(assignment.PermissionId))
            {
                _dbContext.OrganizationRolePermissions.Remove(assignment);
                removed++;
            }
        }

        var existingIds = existingAssignments
            .Where(assignment => desiredIds.Contains(assignment.PermissionId))
            .Select(assignment => assignment.PermissionId)
            .ToHashSet();

        var tenantId = organization.TenantId ?? role.TenantId;
        var added = 0;
        foreach (var permissionId in desiredIds)
        {
            if (existingIds.Contains(permissionId))
            {
                continue;
            }

            _dbContext.OrganizationRolePermissions.Add(new OrganizationRolePermission
            {
                Id = Guid.NewGuid(),
                RoleId = roleId,
                PermissionId = permissionId,
                OrganizationId = organizationId,
                TenantId = tenantId,
                CreatedAtUtc = DateTimeOffset.UtcNow
            });
            added++;
        }

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger?.LogInformation(
            "Updated organization role permissions for Role {RoleId} in organization {OrganizationId}. Added {Added}, Removed {Removed}.",
            roleId,
            organizationId,
            added,
            removed);
    }

    private async Task EnsureRoleNameIsUniqueAsync(Guid? tenantId, Guid? organizationId, string roleName, CancellationToken cancellationToken)
    {
        var query = _dbContext.OrganizationRoles.AsNoTracking()
            .Where(role => role.Name == roleName);

        if (tenantId.HasValue)
        {
            query = query.Where(role => role.TenantId == tenantId.Value);
        }
        else
        {
            query = query.Where(role => role.TenantId == null);
        }

        if (organizationId.HasValue)
        {
            query = query.Where(role => role.OrganizationId == organizationId.Value);
        }
        else
        {
            query = query.Where(role => role.OrganizationId == null);
        }

        var exists = await query.AnyAsync(cancellationToken).ConfigureAwait(false);
        if (exists)
        {
            throw new InvalidOperationException($"Role '{roleName}' already exists for the specified scope.");
        }
    }
}
