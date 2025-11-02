using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Identity.Base.Identity;
using Identity.Base.Organisations.Data;
using Identity.Base.Organisations.Domain;
using Identity.Base.Organisations.Options;
using Identity.Base.Roles.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Identity.Base.Organisations.Services;

public sealed class OrganisationRoleSeeder
{
    private readonly OrganisationDbContext _dbContext;
    private readonly IRoleDbContext? _roleDbContext;
    private readonly OrganisationRoleOptions _options;
    private readonly IdentityBaseSeedCallbacks _seedCallbacks;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OrganisationRoleSeeder>? _logger;

    public OrganisationRoleSeeder(
        OrganisationDbContext dbContext,
        IOptions<OrganisationRoleOptions> options,
        IdentityBaseSeedCallbacks seedCallbacks,
        IServiceProvider serviceProvider,
        ILogger<OrganisationRoleSeeder>? logger = null,
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
        var definitions = (_options.DefaultRoles ?? new List<OrganisationRoleDefinitionOptions>())
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
                "Seeded organisation roles. Created: {Created}, Updated: {Updated}, Permissions Added: {Added}, Permissions Removed: {Removed}",
                createdCount,
                updatedCount,
                permissionsAdded,
                permissionsRemoved);
        }

        foreach (var callback in _seedCallbacks.OrganisationSeedCallbacks)
        {
            await callback(_serviceProvider, cancellationToken).ConfigureAwait(false);
        }
    }

    private static OrganisationRoleDefinitionOptions NormalizeDefinition(OrganisationRoleDefinitionOptions definition)
    {
        var normalized = new OrganisationRoleDefinitionOptions
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
        OrganisationRoleDefinitionOptions definition,
        DateTimeOffset timestamp,
        CancellationToken cancellationToken)
    {
        var role = await _dbContext.OrganisationRoles
            .FirstOrDefaultAsync(entity =>
                entity.OrganisationId == null &&
                entity.TenantId == null &&
                EF.Functions.ILike(entity.Name, definition.Name),
                cancellationToken)
            .ConfigureAwait(false);

        if (role is null)
        {
            role = new OrganisationRole
            {
                Id = Guid.NewGuid(),
                Name = definition.Name,
                Description = definition.Description,
                IsSystemRole = definition.IsSystemRole,
                CreatedAtUtc = timestamp
            };

            _dbContext.OrganisationRoles.Add(role);
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
        OrganisationRole role,
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
                "Skipping permission seeding for organisation role {RoleName} because no role database is configured.",
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
                "One or more permissions referenced by organisation role {RoleName} were not found: {Permissions}",
                role.Name,
                string.Join(", ", missing));
        }

        var desiredIds = permissions
            .Select(permission => permission.Id)
            .ToHashSet();

        var scopedPermissions = await _dbContext.OrganisationRolePermissions
            .Where(assignment =>
                assignment.RoleId == role.Id &&
                assignment.OrganisationId == role.OrganisationId &&
                assignment.TenantId == role.TenantId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var removed = 0;
        foreach (var assignment in scopedPermissions)
        {
            if (!desiredIds.Contains(assignment.PermissionId))
            {
                _dbContext.OrganisationRolePermissions.Remove(assignment);
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

            _dbContext.OrganisationRolePermissions.Add(new OrganisationRolePermission
            {
                Id = Guid.NewGuid(),
                RoleId = role.Id,
                PermissionId = permission.Id,
                TenantId = role.TenantId,
                OrganisationId = role.OrganisationId,
                CreatedAtUtc = timestamp
            });
            added++;
        }

        return new PermissionSeedResult(added, removed);
    }

    private async Task<PermissionSeedResult> RemoveAllPermissionsAsync(
        OrganisationRole role,
        CancellationToken cancellationToken)
    {
        var scopedPermissions = await _dbContext.OrganisationRolePermissions
            .Where(assignment =>
                assignment.RoleId == role.Id &&
                assignment.OrganisationId == role.OrganisationId &&
                assignment.TenantId == role.TenantId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (scopedPermissions.Count == 0)
        {
            return new PermissionSeedResult(0, 0);
        }

        _dbContext.OrganisationRolePermissions.RemoveRange(scopedPermissions);
        return new PermissionSeedResult(0, scopedPermissions.Count);
    }

    private readonly record struct RoleSeedResult(OrganisationRole Role, bool Created, bool Updated);

    private readonly record struct PermissionSeedResult(int Added, int Removed);
}
