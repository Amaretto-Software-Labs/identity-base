using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Identity.Base.Organisations.Data;
using Identity.Base.Roles.Abstractions;
using Identity.Base.Roles.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Identity.Base.Organisations.Services;

public sealed class OrganisationPermissionResolver : IOrganisationPermissionResolver
{
    private readonly OrganisationDbContext _organisationDbContext;
    private readonly IRoleDbContext? _roleDbContext;
    private readonly IRoleAssignmentService _roleAssignmentService;
    private readonly ILogger<OrganisationPermissionResolver>? _logger;

    public OrganisationPermissionResolver(
        OrganisationDbContext organisationDbContext,
        IRoleAssignmentService roleAssignmentService,
        ILogger<OrganisationPermissionResolver>? logger = null,
        IRoleDbContext? roleDbContext = null)
    {
        _organisationDbContext = organisationDbContext ?? throw new ArgumentNullException(nameof(organisationDbContext));
        _roleAssignmentService = roleAssignmentService ?? throw new ArgumentNullException(nameof(roleAssignmentService));
        _logger = logger;
        _roleDbContext = roleDbContext;
    }

    public async Task<IReadOnlyList<string>> GetPermissionsAsync(Guid organisationId, Guid userId, CancellationToken cancellationToken = default)
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

        if (organisationId == Guid.Empty)
        {
            return permissions.Count == 0 ? Array.Empty<string>() : permissions.ToList();
        }

        var organisationPermissions = await GetOrganisationPermissionsAsync(organisationId, userId, cancellationToken).ConfigureAwait(false);
        foreach (var permission in organisationPermissions)
        {
            if (!string.IsNullOrWhiteSpace(permission))
            {
                permissions.Add(permission.Trim());
            }
        }

        return permissions.Count == 0 ? Array.Empty<string>() : permissions.ToList();
    }

    public async Task<IReadOnlyList<string>> GetOrganisationPermissionsAsync(Guid organisationId, Guid userId, CancellationToken cancellationToken = default)
    {
        if (organisationId == Guid.Empty || userId == Guid.Empty)
        {
            return Array.Empty<string>();
        }

        var membership = await _organisationDbContext.OrganisationMemberships
            .AsNoTracking()
            .Where(entity => entity.OrganisationId == organisationId && entity.UserId == userId)
            .Select(entity => new { entity.TenantId })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (membership is null)
        {
            return Array.Empty<string>();
        }

        var roleIds = await _organisationDbContext.OrganisationRoleAssignments
            .AsNoTracking()
            .Where(assignment => assignment.OrganisationId == organisationId && assignment.UserId == userId)
            .Select(assignment => assignment.RoleId)
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (roleIds.Count == 0)
        {
            return Array.Empty<string>();
        }

        var scopedPermissionsQuery = _organisationDbContext.OrganisationRolePermissions
            .AsNoTracking()
            .Where(permission => roleIds.Contains(permission.RoleId))
            .Where(permission => permission.OrganisationId == null || permission.OrganisationId == organisationId);

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
                "Organisation-specific permissions are configured but no role database is available to resolve permission names.");
            return Array.Empty<string>();
        }

        var organisationPermissions = await _roleDbContext.Permissions
            .AsNoTracking()
            .Where(permission => permissionIds.Contains(permission.Id))
            .Select(permission => permission.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return organisationPermissions;
    }
}
