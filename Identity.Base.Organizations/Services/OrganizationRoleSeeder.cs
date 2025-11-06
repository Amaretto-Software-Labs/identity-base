using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Identity.Base.Identity;
using Identity.Base.Organizations.Data;
using Identity.Base.Organizations.Domain;
using Identity.Base.Organizations.Options;
using Identity.Base.Roles.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Identity.Base.Organizations.Services;

public sealed class OrganizationRoleSeeder
{
    private readonly OrganizationDbContext _dbContext;
    private readonly IRoleDbContext? _roleDbContext;
    private readonly OrganizationRoleOptions _options;
    private readonly IdentityBaseSeedCallbacks _seedCallbacks;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OrganizationRoleSeeder>? _logger;

    public OrganizationRoleSeeder(
        OrganizationDbContext dbContext,
        IOptions<OrganizationRoleOptions> options,
        IdentityBaseSeedCallbacks seedCallbacks,
        IServiceProvider serviceProvider,
        ILogger<OrganizationRoleSeeder>? logger = null,
        IRoleDbContext? roleDbContext = null)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _roleDbContext = roleDbContext;
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _seedCallbacks = seedCallbacks ?? throw new ArgumentNullException(nameof(seedCallbacks));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var definitions = (_options.DefaultRoles ?? new List<OrganizationRoleDefinitionOptions>())
            .Where(definition => !string.IsNullOrWhiteSpace(definition.Name))
            .Select(definition => NormalizeDefinition(definition))
            .ToList();

        var now = DateTimeOffset.UtcNow;
        var createdCount = 0;
        var updatedCount = 0;
        var permissionsAdded = 0;
        var permissionsRemoved = 0;

        foreach (var definition in definitions)
        {
            var result = await EnsureRoleAsync(definition, now, cancellationToken).ConfigureAwait(false);
            createdCount += result.Created ? 1 : 0;
            updatedCount += result.Updated ? 1 : 0;

            var permissionResult = await EnsureRolePermissionsAsync(result.Role, definition.Permissions, now, cancellationToken).ConfigureAwait(false);
            permissionsAdded += permissionResult.Added;
            permissionsRemoved += permissionResult.Removed;
        }

        if (createdCount > 0 || updatedCount > 0 || permissionsAdded > 0 || permissionsRemoved > 0)
        {
            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            _logger?.LogInformation(
                "Seeded organization roles. Created: {Created}, Updated: {Updated}, Permissions Added: {Added}, Permissions Removed: {Removed}",
                createdCount,
                updatedCount,
                permissionsAdded,
                permissionsRemoved);
        }

        foreach (var callback in _seedCallbacks.OrganizationSeedCallbacks)
        {
            await callback(_serviceProvider, cancellationToken).ConfigureAwait(false);
        }
    }

    private static OrganizationRoleDefinitionOptions NormalizeDefinition(OrganizationRoleDefinitionOptions definition)
    {
        var normalized = new OrganizationRoleDefinitionOptions
        {
            DefaultType = definition.DefaultType,
            Description = definition.Description?.Trim(),
            IsSystemRole = definition.IsSystemRole,
            Name = definition.Name.Trim()
        };

        if (definition.Permissions is { Count: > 0 })
        {
            normalized.Permissions = definition.Permissions
                .Where(permission => !string.IsNullOrWhiteSpace(permission))
                .Select(permission => permission.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        return normalized;
    }

    private async Task<RoleSeedResult> EnsureRoleAsync(
        OrganizationRoleDefinitionOptions definition,
        DateTimeOffset timestamp,
        CancellationToken cancellationToken)
    {
        var baseQuery = _dbContext.OrganizationRoles
            .Where(entity =>
                entity.OrganizationId == null &&
                entity.TenantId == null);

        OrganizationRole? role;
        if (_dbContext.Database.ProviderName?.Contains("InMemory", StringComparison.OrdinalIgnoreCase) == true)
        {
            role = baseQuery
                .AsEnumerable()
                .FirstOrDefault(entity => string.Equals(entity.Name, definition.Name, StringComparison.OrdinalIgnoreCase));
        }
        else
        {
            role = await baseQuery
                .FirstOrDefaultAsync(entity => EF.Functions.ILike(entity.Name, definition.Name), cancellationToken)
                .ConfigureAwait(false);
        }

        if (role is null)
        {
            role = new OrganizationRole
            {
                Id = Guid.NewGuid(),
                Name = definition.Name,
                Description = definition.Description,
                IsSystemRole = definition.IsSystemRole,
                CreatedAtUtc = timestamp
            };

            _dbContext.OrganizationRoles.Add(role);
            return new RoleSeedResult(role, true, false);
        }

        var updated = false;

        if (!string.Equals(role.Description, definition.Description, StringComparison.Ordinal))
        {
            role.Description = definition.Description;
            updated = true;
        }

        if (role.IsSystemRole != definition.IsSystemRole)
        {
            role.IsSystemRole = definition.IsSystemRole;
            updated = true;
        }

        if (updated)
        {
            role.UpdatedAtUtc = timestamp;
        }

        return new RoleSeedResult(role, false, updated);
    }

    private async Task<PermissionSeedResult> EnsureRolePermissionsAsync(
        OrganizationRole role,
        IReadOnlyCollection<string> permissionNames,
        DateTimeOffset timestamp,
        CancellationToken cancellationToken)
    {
        if (permissionNames is null || permissionNames.Count == 0)
        {
            return await RemoveAllPermissionsAsync(role, cancellationToken).ConfigureAwait(false);
        }

        if (_roleDbContext is null)
        {
            _logger?.LogWarning(
                "Skipping permission seeding for organization role {RoleName} because no role database is configured.",
                role.Name);
            return new PermissionSeedResult(0, 0);
        }

        var desired = permissionNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (desired.Count == 0)
        {
            return await RemoveAllPermissionsAsync(role, cancellationToken).ConfigureAwait(false);
        }

        var permissions = await _roleDbContext.Permissions
            .Where(permission => desired.Contains(permission.Name))
            .Select(permission => new { permission.Id, permission.Name })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var missing = desired.Except(permissions.Select(permission => permission.Name), StringComparer.OrdinalIgnoreCase).ToList();
        if (missing.Count > 0)
        {
            _logger?.LogWarning(
                "One or more permissions referenced by organization role {RoleName} were not found: {Permissions}",
                role.Name,
                string.Join(", ", missing));
        }

        var desiredIds = permissions
            .Select(permission => permission.Id)
            .ToHashSet();

        var scopedPermissions = await _dbContext.OrganizationRolePermissions
            .Where(assignment =>
                assignment.RoleId == role.Id &&
                assignment.OrganizationId == role.OrganizationId &&
                assignment.TenantId == role.TenantId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var removed = 0;
        foreach (var assignment in scopedPermissions)
        {
            if (!desiredIds.Contains(assignment.PermissionId))
            {
                _dbContext.OrganizationRolePermissions.Remove(assignment);
                removed++;
            }
        }

        var added = 0;
        foreach (var permission in permissions)
        {
            var alreadyAssigned = scopedPermissions.Any(existing => existing.PermissionId == permission.Id);
            if (alreadyAssigned)
            {
                continue;
            }

            _dbContext.OrganizationRolePermissions.Add(new OrganizationRolePermission
            {
                Id = Guid.NewGuid(),
                RoleId = role.Id,
                PermissionId = permission.Id,
                TenantId = role.TenantId,
                OrganizationId = role.OrganizationId,
                CreatedAtUtc = timestamp
            });
            added++;
        }

        return new PermissionSeedResult(added, removed);
    }

    private async Task<PermissionSeedResult> RemoveAllPermissionsAsync(
        OrganizationRole role,
        CancellationToken cancellationToken)
    {
        var scopedPermissions = await _dbContext.OrganizationRolePermissions
            .Where(assignment =>
                assignment.RoleId == role.Id &&
                assignment.OrganizationId == role.OrganizationId &&
                assignment.TenantId == role.TenantId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (scopedPermissions.Count == 0)
        {
            return new PermissionSeedResult(0, 0);
        }

        _dbContext.OrganizationRolePermissions.RemoveRange(scopedPermissions);
        return new PermissionSeedResult(0, scopedPermissions.Count);
    }

    private readonly record struct RoleSeedResult(OrganizationRole Role, bool Created, bool Updated);

    private readonly record struct PermissionSeedResult(int Added, int Removed);
}
