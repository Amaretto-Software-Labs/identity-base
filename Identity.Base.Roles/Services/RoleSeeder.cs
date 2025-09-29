using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Identity.Base.Identity;
using Identity.Base.Roles.Abstractions;
using Identity.Base.Roles.Entities;
using Identity.Base.Roles.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Identity.Base.Roles.Services;

public sealed class RoleSeeder : IRoleSeeder
{
    private readonly IRoleDbContext _dbContext;
    private readonly RoleConfigurationOptions _roleOptions;
    private readonly PermissionCatalogOptions _permissionOptions;
    private readonly ILogger<RoleSeeder> _logger;
    private readonly IdentityBaseSeedCallbacks _seedCallbacks;
    private readonly IServiceProvider _serviceProvider;

    public RoleSeeder(
        IRoleDbContext dbContext,
        IOptions<RoleConfigurationOptions> roleOptions,
        IOptions<PermissionCatalogOptions> permissionOptions,
        ILogger<RoleSeeder> logger,
        IdentityBaseSeedCallbacks seedCallbacks,
        IServiceProvider serviceProvider)
    {
        _dbContext = dbContext;
        _roleOptions = roleOptions.Value;
        _permissionOptions = permissionOptions.Value;
        _logger = logger;
        _seedCallbacks = seedCallbacks;
        _serviceProvider = serviceProvider;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (!_dbContext.Database.IsRelational())
        {
            await SeedInternalAsync(cancellationToken).ConfigureAwait(false);
            await ExecuteCallbacksAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        var strategy = _dbContext.Database.CreateExecutionStrategy();

        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            await SeedInternalAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }).ConfigureAwait(false);

        await ExecuteCallbacksAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task SeedInternalAsync(CancellationToken cancellationToken)
    {
        var permissionEntities = await _dbContext.Permissions
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var roleEntities = await _dbContext.Roles
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var existingPermissionNames = new HashSet<string>(permissionEntities.Select(p => p.Name), StringComparer.OrdinalIgnoreCase);
        var existingRoleNames = new HashSet<string>(roleEntities.Select(r => r.Name), StringComparer.OrdinalIgnoreCase);

        var hasAllPermissions = _permissionOptions.Definitions.All(def => existingPermissionNames.Contains(def.Name));
        var hasAllRoles = _roleOptions.Definitions.All(def => existingRoleNames.Contains(def.Name));

        if (hasAllPermissions && hasAllRoles)
        {
            return;
        }

        var existingPermissions = new Dictionary<string, Permission>(StringComparer.OrdinalIgnoreCase);
        var permissionsById = new Dictionary<Guid, Permission>();

        foreach (var permission in permissionEntities)
        {
            if (existingPermissions.TryAdd(permission.Name, permission))
            {
                permissionsById[permission.Id] = permission;
            }
        }

        foreach (var permissionDefinition in _permissionOptions.Definitions)
        {
            if (existingPermissions.ContainsKey(permissionDefinition.Name))
            {
                continue;
            }

            var permission = new Permission
            {
                Name = permissionDefinition.Name,
                Description = permissionDefinition.Description
            };

            _dbContext.Permissions.Add(permission);
            existingPermissions[permission.Name] = permission;
            permissionsById[permission.Id] = permission;
        }

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var existingRoles = new Dictionary<string, Role>(StringComparer.OrdinalIgnoreCase);
        foreach (var roleEntity in roleEntities)
        {
            existingRoles.TryAdd(roleEntity.Name, roleEntity);
        }

        foreach (var roleDefinition in _roleOptions.Definitions)
        {
            if (!existingRoles.TryGetValue(roleDefinition.Name, out var role))
            {
                role = new Role
                {
                    Name = roleDefinition.Name,
                    Description = roleDefinition.Description,
                    IsSystemRole = roleDefinition.IsSystemRole,
                };

                _dbContext.Roles.Add(role);
                existingRoles[role.Name] = role;
            }
            else
            {
                role.Description = roleDefinition.Description;
                role.IsSystemRole = roleDefinition.IsSystemRole;
            }

            var existingRolePermissions = await _dbContext.RolePermissions
                .Where(rp => rp.RoleId == role.Id)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            var desiredPermissions = new HashSet<string>(roleDefinition.Permissions, StringComparer.OrdinalIgnoreCase);

            foreach (var rolePermission in existingRolePermissions)
            {
                if (!permissionsById.TryGetValue(rolePermission.PermissionId, out var permission))
                {
                    continue;
                }

                if (!desiredPermissions.Contains(permission.Name))
                {
                    _dbContext.RolePermissions.Remove(rolePermission);
                }
            }

            foreach (var permissionName in desiredPermissions)
            {
                if (!existingPermissions.TryGetValue(permissionName, out var permission))
                {
                    _logger.LogWarning("Permission {Permission} referenced by role {Role} but not defined.", permissionName, roleDefinition.Name);
                    continue;
                }

                var alreadyAssigned = existingRolePermissions.Any(rp => rp.PermissionId == permission.Id);

                if (!alreadyAssigned)
                {
                    _dbContext.RolePermissions.Add(new RolePermission
                    {
                        RoleId = role.Id,
                        PermissionId = permission.Id
                    });
                }
            }

            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task ExecuteCallbacksAsync(CancellationToken cancellationToken)
    {
        foreach (var callback in _seedCallbacks.RoleSeedCallbacks)
        {
            await callback(_serviceProvider, cancellationToken).ConfigureAwait(false);
        }
    }
}
