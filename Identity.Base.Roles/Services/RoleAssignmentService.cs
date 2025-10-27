using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Identity.Base.Roles.Abstractions;
using Identity.Base.Roles.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Identity.Base.Roles.Services;

public sealed class RoleAssignmentService : IRoleAssignmentService
{
    private readonly IRoleDbContext _dbContext;
    private readonly ILogger<RoleAssignmentService> _logger;
    private readonly IRoleSeeder? _roleSeeder;

    public RoleAssignmentService(IRoleDbContext dbContext, ILogger<RoleAssignmentService> logger, IRoleSeeder? roleSeeder = null)
    {
        _dbContext = dbContext;
        _logger = logger;
        _roleSeeder = roleSeeder;
    }

    public async Task AssignRolesAsync(Guid userId, IEnumerable<string> roleNames, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(roleNames);

        var desiredRoles = roleNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var roles = await _dbContext.Roles
            .Where(role => desiredRoles.Contains(role.Name))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (roles.Count != desiredRoles.Count)
        {
            var missing = desiredRoles.Except(roles.Select(r => r.Name), StringComparer.OrdinalIgnoreCase).ToList();

            if (_roleSeeder is not null && missing.Count > 0)
            {
                await _roleSeeder.SeedAsync(cancellationToken).ConfigureAwait(false);

                roles = await _dbContext.Roles
                    .Where(role => desiredRoles.Contains(role.Name))
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
            }

            if (roles.Count != desiredRoles.Count)
            {
                var stillMissing = desiredRoles.Except(roles.Select(r => r.Name), StringComparer.OrdinalIgnoreCase);
                throw new InvalidOperationException($"Unknown roles: {string.Join(", ", stillMissing)}");
            }
        }

        var existingAssignments = await _dbContext.UserRoles
            .Where(ur => ur.UserId == userId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // Remove assignments not desired anymore
        foreach (var assignment in existingAssignments)
        {
            if (!roles.Any(role => role.Id == assignment.RoleId))
            {
                _dbContext.UserRoles.Remove(assignment);
            }
        }

        // Add new assignments
        var currentRoleIds = existingAssignments.Select(ur => ur.RoleId).ToHashSet();
        foreach (var role in roles)
        {
            if (currentRoleIds.Add(role.Id))
            {
                _dbContext.UserRoles.Add(new UserRole
                {
                    UserId = userId,
                    RoleId = role.Id
                });
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<string>> GetUserRoleNamesAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var roles = await _dbContext.UserRoles
            .Where(ur => ur.UserId == userId)
            .Join(_dbContext.Roles, ur => ur.RoleId, role => role.Id, (_, role) => role.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return roles;
    }

    public async Task<IReadOnlyList<string>> GetEffectivePermissionsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var permissions = await _dbContext.UserRoles
            .Where(ur => ur.UserId == userId)
            .Join(_dbContext.RolePermissions, ur => ur.RoleId, rp => rp.RoleId, (ur, rp) => rp.PermissionId)
            .Join(_dbContext.Permissions, permissionId => permissionId, permission => permission.Id, (_, permission) => permission.Name)
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return permissions;
    }
}
