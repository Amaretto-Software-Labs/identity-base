using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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

    public RoleSeeder(
        IRoleDbContext dbContext,
        IOptions<RoleConfigurationOptions> roleOptions,
        IOptions<PermissionCatalogOptions> permissionOptions,
        ILogger<RoleSeeder> logger)
    {
        _dbContext = dbContext;
        _roleOptions = roleOptions.Value;
        _permissionOptions = permissionOptions.Value;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        var existingPermissions = await _dbContext.Permissions
            .ToDictionaryAsync(p => p.Name, p => p, StringComparer.OrdinalIgnoreCase, cancellationToken)
            .ConfigureAwait(false);
        var permissionsById = existingPermissions.Values.ToDictionary(p => p.Id);

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

        var existingRoles = await _dbContext.Roles
            .ToDictionaryAsync(r => r.Name, r => r, StringComparer.OrdinalIgnoreCase, cancellationToken)
            .ConfigureAwait(false);

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

                await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
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

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }
}
