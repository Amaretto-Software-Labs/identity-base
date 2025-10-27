using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Identity.Base.Organizations.Data;
using Identity.Base.Roles.Abstractions;
using Identity.Base.Roles.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Identity.Base.Organizations.Services;

public sealed class OrganizationPermissionResolver : IOrganizationPermissionResolver
{
    private readonly OrganizationDbContext _organizationDbContext;
    private readonly IRoleDbContext? _roleDbContext;
    private readonly IRoleAssignmentService _roleAssignmentService;
    private readonly ILogger<OrganizationPermissionResolver>? _logger;

    public OrganizationPermissionResolver(
        OrganizationDbContext organizationDbContext,
        IRoleAssignmentService roleAssignmentService,
        ILogger<OrganizationPermissionResolver>? logger = null,
        IRoleDbContext? roleDbContext = null)
    {
        _organizationDbContext = organizationDbContext ?? throw new ArgumentNullException(nameof(organizationDbContext));
        _roleAssignmentService = roleAssignmentService ?? throw new ArgumentNullException(nameof(roleAssignmentService));
        _logger = logger;
        _roleDbContext = roleDbContext;
    }

    public async Task<IReadOnlyList<string>> GetPermissionsAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            return Array.Empty<string>();
        }

        var permissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var globalPermissions = await _roleAssignmentService
            .GetEffectivePermissionsAsync(userId, cancellationToken)
            .ConfigureAwait(false);

        foreach (var permission in globalPermissions)
        {
            if (!string.IsNullOrWhiteSpace(permission))
            {
                permissions.Add(permission.Trim());
            }
        }

        if (organizationId == Guid.Empty)
        {
            return permissions.Count == 0 ? Array.Empty<string>() : permissions.ToList();
        }

        var organizationPermissions = await GetOrganizationPermissionsAsync(organizationId, userId, cancellationToken).ConfigureAwait(false);
        foreach (var permission in organizationPermissions)
        {
            if (!string.IsNullOrWhiteSpace(permission))
            {
                permissions.Add(permission.Trim());
            }
        }

        return permissions.Count == 0 ? Array.Empty<string>() : permissions.ToList();
    }

    public async Task<IReadOnlyList<string>> GetOrganizationPermissionsAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken = default)
    {
        if (organizationId == Guid.Empty || userId == Guid.Empty)
        {
            return Array.Empty<string>();
        }

        var membership = await _organizationDbContext.OrganizationMemberships
            .AsNoTracking()
            .Where(entity => entity.OrganizationId == organizationId && entity.UserId == userId)
            .Select(entity => new { entity.TenantId })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (membership is null)
        {
            return Array.Empty<string>();
        }

        var roleIds = await _organizationDbContext.OrganizationRoleAssignments
            .AsNoTracking()
            .Where(assignment => assignment.OrganizationId == organizationId && assignment.UserId == userId)
            .Select(assignment => assignment.RoleId)
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (roleIds.Count == 0)
        {
            return Array.Empty<string>();
        }

        var scopedPermissionsQuery = _organizationDbContext.OrganizationRolePermissions
            .AsNoTracking()
            .Where(permission => roleIds.Contains(permission.RoleId))
            .Where(permission => permission.OrganizationId == null || permission.OrganizationId == organizationId);

        if (membership.TenantId.HasValue)
        {
            var tenantId = membership.TenantId.Value;
            scopedPermissionsQuery = scopedPermissionsQuery.Where(permission => permission.TenantId == null || permission.TenantId == tenantId);
        }
        else
        {
            scopedPermissionsQuery = scopedPermissionsQuery.Where(permission => permission.TenantId == null);
        }

        var permissionIds = await scopedPermissionsQuery
            .Select(permission => permission.PermissionId)
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (permissionIds.Count == 0)
        {
            return Array.Empty<string>();
        }

        if (_roleDbContext is null)
        {
            _logger?.LogWarning(
                "Organization-specific permissions are configured but no role database is available to resolve permission names.");
            return Array.Empty<string>();
        }

        var organizationPermissions = await _roleDbContext.Permissions
            .AsNoTracking()
            .Where(permission => permissionIds.Contains(permission.Id))
            .Select(permission => permission.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return organizationPermissions;
    }
}
