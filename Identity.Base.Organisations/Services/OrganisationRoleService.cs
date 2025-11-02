using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Identity.Base.Organisations.Abstractions;
using Identity.Base.Organisations.Data;
using Identity.Base.Organisations.Domain;
using Identity.Base.Organisations.Options;
using Identity.Base.Roles.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Identity.Base.Organisations.Services;

public sealed class OrganisationRoleService : IOrganisationRoleService
{
    private readonly OrganisationDbContext _dbContext;
    private readonly IRoleDbContext _roleDbContext;
    private readonly OrganisationRoleOptions _options;
    private readonly ILogger<OrganisationRoleService>? _logger;

    public OrganisationRoleService(
        OrganisationDbContext dbContext,
        IRoleDbContext roleDbContext,
        IOptions<OrganisationRoleOptions> options,
        ILogger<OrganisationRoleService>? logger = null)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _roleDbContext = roleDbContext ?? throw new ArgumentNullException(nameof(roleDbContext));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger;
    }

    public async Task<OrganisationRole> CreateAsync(OrganisationRoleCreateRequest request, CancellationToken cancellationToken = default)
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
        if (request.OrganisationId.HasValue)
        {
            var organisation = await _dbContext.Organisations
                .AsNoTracking()
                .FirstOrDefaultAsync(org => org.Id == request.OrganisationId.Value, cancellationToken)
                .ConfigureAwait(false);

            if (organisation is null)
            {
                throw new KeyNotFoundException($"Organisation {request.OrganisationId.Value} was not found.");
            }

            if (tenantId.HasValue && organisation.TenantId.HasValue && tenantId.Value != organisation.TenantId.Value)
            {
                throw new InvalidOperationException("Organisation and role tenants do not match.");
            }

            tenantId ??= organisation.TenantId;
        }

        await EnsureRoleNameIsUniqueAsync(tenantId, request.OrganisationId, name, cancellationToken).ConfigureAwait(false);

        var role = new OrganisationRole
        {
            Id = Guid.NewGuid(),
            OrganisationId = request.OrganisationId,
            TenantId = tenantId,
            Name = name,
            Description = request.Description?.Trim(),
            IsSystemRole = request.IsSystemRole,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        _dbContext.OrganisationRoles.Add(role);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger?.LogInformation(
            "Created organisation role {RoleId} (Name: {RoleName}) for organisation {OrganisationId}",
            role.Id,
            role.Name,
            role.OrganisationId);

        return role;
    }

    public async Task<OrganisationRole?> GetByIdAsync(Guid roleId, CancellationToken cancellationToken = default)
    {
        if (roleId == Guid.Empty)
        {
            return null;
        }

        return await _dbContext.OrganisationRoles
            .AsNoTracking()
            .FirstOrDefaultAsync(role => role.Id == roleId, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<OrganisationRole>> ListAsync(Guid? tenantId, Guid? organisationId, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.OrganisationRoles.AsNoTracking().AsQueryable();

        if (tenantId.HasValue)
        {
            query = query.Where(role => role.TenantId == tenantId.Value || role.TenantId == null);
        }
        else
        {
            query = query.Where(role => role.TenantId == null);
        }

        if (organisationId.HasValue)
        {
            query = query.Where(role => role.OrganisationId == organisationId.Value || role.OrganisationId == null);
        }

        return await query
            .OrderBy(role => role.OrganisationId.HasValue ? 1 : 0)
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

        var role = await _dbContext.OrganisationRoles
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

        _dbContext.OrganisationRoles.Remove(role);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger?.LogInformation("Deleted organisation role {RoleId}", roleId);
    }

    public async Task<OrganisationRolePermissionSet> GetPermissionsAsync(Guid roleId, Guid organisationId, CancellationToken cancellationToken = default)
    {
        if (roleId == Guid.Empty)
        {
            throw new ArgumentException("Role identifier is required.", nameof(roleId));
        }

        if (organisationId == Guid.Empty)
        {
            throw new ArgumentException("Organisation identifier is required.", nameof(organisationId));
        }

        var role = await _dbContext.OrganisationRoles
            .AsNoTracking()
            .FirstOrDefaultAsync(entity => entity.Id == roleId, cancellationToken)
            .ConfigureAwait(false);

        if (role is null)
        {
            throw new KeyNotFoundException($"Organisation role {roleId} was not found.");
        }

        var organisation = await _dbContext.Organisations
            .AsNoTracking()
            .FirstOrDefaultAsync(entity => entity.Id == organisationId, cancellationToken)
            .ConfigureAwait(false);

        if (organisation is null)
        {
            throw new KeyNotFoundException($"Organisation {organisationId} was not found.");
        }

        var effectiveIds = new HashSet<Guid>();
        var explicitIds = new HashSet<Guid>();

        var tenantId = organisation.TenantId ?? role.TenantId;

        var permissionQuery = _dbContext.OrganisationRolePermissions
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
            .Where(permission => permission.OrganisationId == organisationId)
            .Select(permission => permission.PermissionId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var id in explicitPermissionIds)
        {
            explicitIds.Add(id);
            effectiveIds.Add(id);
        }

        if (!role.OrganisationId.HasValue || role.OrganisationId.Value != organisationId)
        {
            var inheritedIds = await permissionQuery
                .Where(permission => permission.OrganisationId == role.OrganisationId)
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
            return new OrganisationRolePermissionSet(Array.Empty<string>(), Array.Empty<string>());
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
                    logger?.LogWarning("Permission {PermissionId} referenced by organisation role {RoleId} but not found in catalog.", id, roleId);
                }
            }

            return names.OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToList();
        }

        var effective = MapNames(effectiveIds, nameById, _logger, roleId);
        var explicitNames = MapNames(explicitIds, nameById, _logger, roleId);

        return new OrganisationRolePermissionSet(effective, explicitNames);
    }

    public async Task UpdatePermissionsAsync(Guid roleId, Guid organisationId, IEnumerable<string> permissions, CancellationToken cancellationToken = default)
    {
        if (roleId == Guid.Empty)
        {
            throw new ArgumentException("Role identifier is required.", nameof(roleId));
        }

        if (organisationId == Guid.Empty)
        {
            throw new ArgumentException("Organisation identifier is required.", nameof(organisationId));
        }

        ArgumentNullException.ThrowIfNull(permissions);

        var organisation = await _dbContext.Organisations
            .FirstOrDefaultAsync(entity => entity.Id == organisationId, cancellationToken)
            .ConfigureAwait(false);

        if (organisation is null)
        {
            throw new KeyNotFoundException($"Organisation {organisationId} was not found.");
        }

        var role = await _dbContext.OrganisationRoles
            .FirstOrDefaultAsync(entity => entity.Id == roleId, cancellationToken)
            .ConfigureAwait(false);

        if (role is null)
        {
            throw new KeyNotFoundException($"Organisation role {roleId} was not found.");
        }

        if (role.OrganisationId.HasValue && role.OrganisationId.Value != organisationId)
        {
            throw new InvalidOperationException("Role does not belong to the specified organisation scope.");
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

        var existingAssignments = await _dbContext.OrganisationRolePermissions
            .Where(permission => permission.RoleId == roleId && permission.OrganisationId == organisationId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var removed = 0;
        foreach (var assignment in existingAssignments)
        {
            if (!desiredIds.Contains(assignment.PermissionId))
            {
                _dbContext.OrganisationRolePermissions.Remove(assignment);
                removed++;
            }
        }

        var existingIds = existingAssignments
            .Where(assignment => desiredIds.Contains(assignment.PermissionId))
            .Select(assignment => assignment.PermissionId)
            .ToHashSet();

        var tenantId = organisation.TenantId ?? role.TenantId;
        var added = 0;
        foreach (var permissionId in desiredIds)
        {
            if (existingIds.Contains(permissionId))
            {
                continue;
            }

            _dbContext.OrganisationRolePermissions.Add(new OrganisationRolePermission
            {
                Id = Guid.NewGuid(),
                RoleId = roleId,
                PermissionId = permissionId,
                OrganisationId = organisationId,
                TenantId = tenantId,
                CreatedAtUtc = DateTimeOffset.UtcNow
            });
            added++;
        }

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger?.LogInformation(
            "Updated organisation role permissions for Role {RoleId} in organisation {OrganisationId}. Added {Added}, Removed {Removed}.",
            roleId,
            organisationId,
            added,
            removed);
    }

    private async Task EnsureRoleNameIsUniqueAsync(Guid? tenantId, Guid? organisationId, string roleName, CancellationToken cancellationToken)
    {
        var query = _dbContext.OrganisationRoles.AsNoTracking()
            .Where(role => role.Name == roleName);

        if (tenantId.HasValue)
        {
            query = query.Where(role => role.TenantId == tenantId.Value);
        }
        else
        {
            query = query.Where(role => role.TenantId == null);
        }

        if (organisationId.HasValue)
        {
            query = query.Where(role => role.OrganisationId == organisationId.Value);
        }
        else
        {
            query = query.Where(role => role.OrganisationId == null);
        }

        var exists = await query.AnyAsync(cancellationToken).ConfigureAwait(false);
        if (exists)
        {
            throw new InvalidOperationException($"Role '{roleName}' already exists for the specified scope.");
        }
    }
}
