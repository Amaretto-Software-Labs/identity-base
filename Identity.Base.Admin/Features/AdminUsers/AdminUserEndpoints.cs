using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Identity.Base.Admin.Authorization;
using Identity.Base.Admin.Configuration;
using Identity.Base.Data;
using Identity.Base.Identity;
using Identity.Base.Roles.Abstractions;
using Identity.Base.Roles.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;

namespace Identity.Base.Admin.Features.AdminUsers;

public static class AdminUserEndpoints
{
    private const int MaxPageSize = 100;

    public static RouteGroupBuilder MapAdminUserEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints
            .MapGroup("/admin/users")
            .RequireAuthorization(policy => policy.RequireAdminPermission("users.read"))
            .WithTags("Admin.Users");

        group.MapGet("", ListUsersAsync)
            .WithName("AdminListUsers")
            .WithSummary("Returns a paged list of users for administration.")
            .Produces<AdminUserListResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/{id:guid}", GetUserAsync)
            .WithName("AdminGetUser")
            .WithSummary("Returns details for a specific user, including roles and MFA state.")
            .Produces<AdminUserDetailResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        return group;
    }

    private static async Task<IResult> ListUsersAsync(
        [AsParameters] AdminUserListQuery query,
        AppDbContext appDbContext,
        IRoleDbContext roleDbContext,
        CancellationToken cancellationToken)
    {
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize switch
        {
            < 1 => 25,
            > MaxPageSize => MaxPageSize,
            _ => query.PageSize
        };

        var usersQuery = appDbContext.Users.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLowerInvariant();
            usersQuery = usersQuery.Where(user =>
                (user.Email != null && user.Email.ToLower().Contains(term)) ||
                (user.DisplayName != null && user.DisplayName.ToLower().Contains(term)));
        }

        if (query.Locked.HasValue)
        {
            var now = DateTimeOffset.UtcNow;
            if (query.Locked.Value)
            {
                usersQuery = usersQuery.Where(user =>
                    user.LockoutEnabled && user.LockoutEnd.HasValue && user.LockoutEnd > now);
            }
            else
            {
                usersQuery = usersQuery.Where(user =>
                    !user.LockoutEnabled || !user.LockoutEnd.HasValue || user.LockoutEnd <= now);
            }
        }

        if (!string.IsNullOrWhiteSpace(query.Role))
        {
            var roleName = query.Role.Trim();
            var roleId = await roleDbContext.Roles
                .Where(role => role.Name == roleName)
                .Select(role => role.Id)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);

            if (roleId == Guid.Empty)
            {
                return Results.Ok(AdminUserListResponse.Empty(page, pageSize));
            }

            var userIdsWithRole = await roleDbContext.UserRoles
                .Where(userRole => userRole.RoleId == roleId)
                .Select(userRole => userRole.UserId)
                .Distinct()
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            if (userIdsWithRole.Count == 0)
            {
                return Results.Ok(AdminUserListResponse.Empty(page, pageSize));
            }

            usersQuery = usersQuery.Where(user => userIdsWithRole.Contains(user.Id));
        }

        var total = await usersQuery.CountAsync(cancellationToken).ConfigureAwait(false);

        var skip = (page - 1) * pageSize;
        var users = await usersQuery
            .OrderBy(user => user.Email ?? user.UserName)
            .ThenBy(user => user.Id)
            .Skip(skip)
            .Take(pageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var userIds = users.Select(user => user.Id).ToList();
        var rolesLookup = new Dictionary<Guid, IReadOnlyList<string>>();

        if (userIds.Count > 0)
        {
            var roleAssignments = await roleDbContext.UserRoles
                .Where(userRole => userIds.Contains(userRole.UserId))
                .Join(
                    roleDbContext.Roles,
                    userRole => userRole.RoleId,
                    role => role.Id,
                    (userRole, role) => new { userRole.UserId, role.Name })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            rolesLookup = roleAssignments
                .GroupBy(entry => entry.UserId)
                .ToDictionary(
                    group => group.Key,
                    group => (IReadOnlyList<string>)group
                        .Select(entry => entry.Name)
                        .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                        .ToList());
        }

        var nowUtc = DateTimeOffset.UtcNow;
        var items = users.Select(user =>
        {
            var roles = rolesLookup.TryGetValue(user.Id, out var value)
                ? value
                : Array.Empty<string>();

            var isLockedOut = user.LockoutEnabled && user.LockoutEnd.HasValue && user.LockoutEnd > nowUtc;

            return new AdminUserSummary(
                user.Id,
                user.Email,
                user.DisplayName,
                user.EmailConfirmed,
                isLockedOut,
                user.CreatedAt,
                user.TwoFactorEnabled,
                roles);
        }).ToList();

        return Results.Ok(new AdminUserListResponse(page, pageSize, total, items));
    }

    private static async Task<IResult> GetUserAsync(
        Guid id,
        UserManager<ApplicationUser> userManager,
        IRoleAssignmentService roleAssignmentService,
        CancellationToken cancellationToken)
    {
        var user = await userManager.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken)
            .ConfigureAwait(false);

        if (user is null)
        {
            return Results.NotFound();
        }

        var roles = await roleAssignmentService
            .GetUserRoleNamesAsync(user.Id, cancellationToken)
            .ConfigureAwait(false);

        var externalLogins = await userManager.GetLoginsAsync(user);
        var loginDtos = externalLogins
            .Select(login => new AdminUserExternalLogin(login.LoginProvider, login.ProviderDisplayName ?? login.LoginProvider, login.ProviderKey))
            .ToList();

        var authenticatorKey = await userManager.GetAuthenticatorKeyAsync(user);
        var isLockedOut = user.LockoutEnabled && user.LockoutEnd.HasValue && user.LockoutEnd > DateTimeOffset.UtcNow;

        var response = new AdminUserDetailResponse(
            user.Id,
            user.Email,
            user.EmailConfirmed,
            user.DisplayName,
            user.CreatedAt,
            user.LockoutEnabled,
            isLockedOut,
            user.LockoutEnd,
            user.TwoFactorEnabled,
            user.PhoneNumberConfirmed,
            user.PhoneNumber,
            user.ProfileMetadata.Values,
            user.ConcurrencyStamp ?? string.Empty,
            roles,
            loginDtos,
            !string.IsNullOrEmpty(authenticatorKey));

        return Results.Ok(response);
    }
}

internal sealed record AdminUserListQuery
{
    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 25;

    public string? Search { get; init; }

    public string? Role { get; init; }

    public bool? Locked { get; init; }
}

internal sealed record AdminUserListResponse(int Page, int PageSize, int TotalCount, IReadOnlyList<AdminUserSummary> Users)
{
    public static AdminUserListResponse Empty(int page, int pageSize)
        => new(page, pageSize, 0, Array.Empty<AdminUserSummary>());
}

internal sealed record AdminUserSummary(
    Guid Id,
    string? Email,
    string? DisplayName,
    bool EmailConfirmed,
    bool IsLockedOut,
    DateTimeOffset CreatedAt,
    bool MfaEnabled,
    IReadOnlyList<string> Roles);

internal sealed record AdminUserDetailResponse(
    Guid Id,
    string? Email,
    bool EmailConfirmed,
    string? DisplayName,
    DateTimeOffset CreatedAt,
    bool LockoutEnabled,
    bool IsLockedOut,
    DateTimeOffset? LockoutEnd,
    bool TwoFactorEnabled,
    bool PhoneNumberConfirmed,
    string? PhoneNumber,
    IReadOnlyDictionary<string, string?> Metadata,
    string ConcurrencyStamp,
    IReadOnlyList<string> Roles,
    IReadOnlyList<AdminUserExternalLogin> ExternalLogins,
    bool AuthenticatorConfigured);

internal sealed record AdminUserExternalLogin(
    string Provider,
    string DisplayName,
    string Key);
