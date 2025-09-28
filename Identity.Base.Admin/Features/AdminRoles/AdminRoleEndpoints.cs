using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Identity.Base.Admin.Authorization;
using Identity.Base.Admin.Configuration;
using Identity.Base.Logging;
using Identity.Base.Roles.Abstractions;
using Identity.Base.Roles.Entities;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Identity.Base.Admin.Features.AdminRoles;

internal static class AdminRoleEndpoints
{
    public static RouteGroupBuilder MapAdminRoleEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints
            .MapGroup("/admin/roles")
            .WithTags("Admin.Roles");

        group.MapGet("", ListRolesAsync)
            .WithName("AdminListRoles")
            .WithSummary("Returns all roles and their permissions.")
            .Produces<IReadOnlyList<AdminRoleSummary>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .RequireAuthorization(policy => policy.RequireAdminPermission("roles.read"));

        group.MapPost("", CreateRoleAsync)
            .WithName("AdminCreateRole")
            .WithSummary("Creates a new role definition with permissions.")
            .Produces<AdminRoleDetail>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .RequireAuthorization(policy => policy.RequireAdminPermission("roles.manage"));

        group.MapPut("/{id:guid}", UpdateRoleAsync)
            .WithName("AdminUpdateRole")
            .WithSummary("Updates role metadata and permissions.")
            .Produces<AdminRoleDetail>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .RequireAuthorization(policy => policy.RequireAdminPermission("roles.manage"));

        group.MapDelete("/{id:guid}", DeleteRoleAsync)
            .WithName("AdminDeleteRole")
            .WithSummary("Deletes a role when not system and unused.")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .RequireAuthorization(policy => policy.RequireAdminPermission("roles.manage"));

        return group;
    }

    private static async Task<IResult> ListRolesAsync(
        IRoleDbContext roleDbContext,
        CancellationToken cancellationToken)
    {
        var roleDtos = await roleDbContext.Roles
            .AsNoTracking()
            .OrderBy(role => role.Name)
            .Select(role => new AdminRoleSummary(
                role.Id,
                role.Name,
                role.Description,
                role.IsSystemRole,
                role.ConcurrencyStamp,
                role.RolePermissions
                    .Select(rp => rp.Permission.Name)
                    .OrderBy(name => name)
                    .ToList(),
                role.UserRoles.Count))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return Results.Ok(roleDtos);
    }

    private static async Task<IResult> CreateRoleAsync(
        AdminRoleCreateRequest request,
        IRoleDbContext roleDbContext,
        IAuditLogger auditLogger,
        CancellationToken cancellationToken)
    {
        request ??= new AdminRoleCreateRequest();

        var validationErrors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        ValidateRoleName(request.Name, validationErrors);

        var normalizedPermissions = NormalizePermissionNames(request.Permissions);
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        if (await roleDbContext.Roles
            .AnyAsync(role => role.Name == request.Name, cancellationToken)
            .ConfigureAwait(false))
        {
            validationErrors[nameof(request.Name)] = new[] { "Role name already exists." };
            return Results.ValidationProblem(validationErrors);
        }

        var permissions = await GetPermissionsAsync(roleDbContext, normalizedPermissions, validationErrors, cancellationToken);
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var role = new Role
        {
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            IsSystemRole = request.IsSystemRole,
        };

        foreach (var permission in permissions)
        {
            role.RolePermissions.Add(new RolePermission
            {
                PermissionId = permission.Id
            });
        }

        roleDbContext.Roles.Add(role);
        await roleDbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await auditLogger.LogAnonymousAsync(
            AuditEventTypes.AdminRoleCreated,
            new { role.Name, role.IsSystemRole, Permissions = normalizedPermissions },
            cancellationToken).ConfigureAwait(false);

        return Results.Created($"/admin/roles/{role.Id:D}", new AdminRoleDetail(
            role.Id,
            role.Name,
            role.Description,
            role.IsSystemRole,
            role.ConcurrencyStamp,
            normalizedPermissions));
    }

    private static async Task<IResult> UpdateRoleAsync(
        Guid id,
        AdminRoleUpdateRequest request,
        IRoleDbContext roleDbContext,
        IAuditLogger auditLogger,
        CancellationToken cancellationToken)
    {
        var role = await roleDbContext.Roles
            .Include(r => r.RolePermissions)
            .ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken)
            .ConfigureAwait(false);

        if (role is null)
        {
            return Results.NotFound();
        }

        if (!string.Equals(role.ConcurrencyStamp, request.ConcurrencyStamp, StringComparison.Ordinal))
        {
            return Results.Problem("Role was modified by another process.", statusCode: StatusCodes.Status409Conflict);
        }

        var validationErrors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(request.Name) && !string.Equals(request.Name, role.Name, StringComparison.OrdinalIgnoreCase))
        {
            ValidateRoleName(request.Name, validationErrors);

            if (await roleDbContext.Roles
                .AnyAsync(r => r.Id != role.Id && r.Name == request.Name, cancellationToken)
                .ConfigureAwait(false))
            {
                validationErrors[nameof(request.Name)] = new[] { "Role name already exists." };
            }

            if (role.IsSystemRole)
            {
                validationErrors[nameof(request.Name)] = new[] { "System roles cannot be renamed." };
            }
        }

        if (role.IsSystemRole && request.IsSystemRole.HasValue && !request.IsSystemRole.Value)
        {
            validationErrors[nameof(request.IsSystemRole)] = new[] { "System roles cannot be downgraded." };
        }

        var normalizedPermissions = NormalizePermissionNames(request.Permissions);
        var permissions = await GetPermissionsAsync(roleDbContext, normalizedPermissions, validationErrors, cancellationToken);
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        if (!string.IsNullOrWhiteSpace(request.Name) && !string.Equals(request.Name, role.Name, StringComparison.OrdinalIgnoreCase) && !role.IsSystemRole)
        {
            role.Name = request.Name.Trim();
        }

        if (!string.IsNullOrWhiteSpace(request.Description))
        {
            role.Description = request.Description.Trim();
        }

        if (request.IsSystemRole.HasValue)
        {
            role.IsSystemRole = request.IsSystemRole.Value;
        }

        role.ConcurrencyStamp = Guid.NewGuid().ToString("N");

        var desiredPermissionIds = new HashSet<Guid>(permissions.Select(p => p.Id));
        foreach (var assignment in role.RolePermissions.ToList())
        {
            if (!desiredPermissionIds.Contains(assignment.PermissionId))
            {
                role.RolePermissions.Remove(assignment);
            }
        }

        var currentPermissionIds = role.RolePermissions.Select(rp => rp.PermissionId).ToHashSet();
        foreach (var permission in permissions)
        {
            if (currentPermissionIds.Add(permission.Id))
            {
                role.RolePermissions.Add(new RolePermission
                {
                    RoleId = role.Id,
                    PermissionId = permission.Id
                });
            }
        }

        await roleDbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await auditLogger.LogAnonymousAsync(
            AuditEventTypes.AdminRoleUpdated,
            new { role.Id, role.Name, role.IsSystemRole, Permissions = normalizedPermissions },
            cancellationToken).ConfigureAwait(false);

        return Results.Ok(new AdminRoleDetail(
            role.Id,
            role.Name,
            role.Description,
            role.IsSystemRole,
            role.ConcurrencyStamp,
            normalizedPermissions));
    }

    private static async Task<IResult> DeleteRoleAsync(
        Guid id,
        IRoleDbContext roleDbContext,
        IAuditLogger auditLogger,
        CancellationToken cancellationToken)
    {
        var role = await roleDbContext.Roles
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken)
            .ConfigureAwait(false);

        if (role is null)
        {
            return Results.NotFound();
        }

        if (role.IsSystemRole)
        {
            return Results.Problem("System roles cannot be deleted.", statusCode: StatusCodes.Status409Conflict);
        }

        var userCount = await roleDbContext.UserRoles
            .CountAsync(r => r.RoleId == role.Id, cancellationToken)
            .ConfigureAwait(false);

        if (userCount > 0)
        {
            return Results.Problem("Role is assigned to users and cannot be deleted.", statusCode: StatusCodes.Status409Conflict);
        }

        roleDbContext.Roles.Remove(role);
        await roleDbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await auditLogger.LogAnonymousAsync(
            AuditEventTypes.AdminRoleDeleted,
            new { role.Id, role.Name },
            cancellationToken).ConfigureAwait(false);

        return Results.NoContent();
    }

    private static void ValidateRoleName(string? name, Dictionary<string, string[]> errors)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            errors[nameof(AdminRoleCreateRequest.Name)] = new[] { "Role name is required." };
            return;
        }

        if (name.Trim().Length > 128)
        {
            errors[nameof(AdminRoleCreateRequest.Name)] = new[] { "Role name exceeds the maximum length of 128 characters." };
        }
    }

    private static IReadOnlyList<string> NormalizePermissionNames(IEnumerable<string>? permissions)
    {
        if (permissions is null)
        {
            return Array.Empty<string>();
        }

        return permissions
            .Where(permission => !string.IsNullOrWhiteSpace(permission))
            .Select(permission => permission.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(permission => permission, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static async Task<List<Permission>> GetPermissionsAsync(
        IRoleDbContext roleDbContext,
        IReadOnlyList<string> permissionNames,
        Dictionary<string, string[]> errors,
        CancellationToken cancellationToken)
    {
        if (permissionNames.Count == 0)
        {
            return new List<Permission>();
        }

        var permissions = await roleDbContext.Permissions
            .Where(permission => permissionNames.Contains(permission.Name))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (permissions.Count != permissionNames.Count)
        {
            var missing = permissionNames.Except(permissions.Select(p => p.Name), StringComparer.OrdinalIgnoreCase).ToArray();
            errors[nameof(AdminRoleCreateRequest.Permissions)] = new[] { $"Unknown permissions: {string.Join(", ", missing)}" };
        }

        return permissions;
    }

    private sealed record AdminRoleSummary(
        Guid Id,
        string Name,
        string? Description,
        bool IsSystemRole,
        string ConcurrencyStamp,
        IReadOnlyList<string> Permissions,
        int UserCount);

    private sealed record AdminRoleDetail(
        Guid Id,
        string Name,
        string? Description,
        bool IsSystemRole,
        string ConcurrencyStamp,
        IReadOnlyList<string> Permissions);

    private sealed class AdminRoleCreateRequest
    {
        public string Name { get; init; } = string.Empty;
        public string? Description { get; init; }
        public bool IsSystemRole { get; init; }
        public IList<string>? Permissions { get; init; }
    }

    private sealed class AdminRoleUpdateRequest
    {
        public string ConcurrencyStamp { get; init; } = string.Empty;
        public string? Name { get; init; }
        public string? Description { get; init; }
        public bool? IsSystemRole { get; init; }
        public IList<string>? Permissions { get; init; }
    }
}
