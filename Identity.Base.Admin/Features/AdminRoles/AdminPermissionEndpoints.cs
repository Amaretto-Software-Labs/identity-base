using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Identity.Base.Admin.Authorization;
using Identity.Base.Admin.Configuration;
using Identity.Base.Roles.Abstractions;

namespace Identity.Base.Admin.Features.AdminRoles;

internal static class AdminPermissionEndpoints
{
    public static RouteGroupBuilder MapAdminPermissionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints
            .MapGroup("/admin/permissions")
            .WithTags("Admin.Permissions");

        group.MapGet("", ListPermissionsAsync)
            .WithName("AdminListPermissions")
            .WithSummary("Returns a paged list of permissions and usage counts.")
            .Produces<AdminPermissionListResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .RequireAuthorization(policy => policy.RequireAdminPermission("roles.read"));

        return group;
    }

    private static async Task<IResult> ListPermissionsAsync(
        [AsParameters] AdminPermissionListQuery query,
        IRoleDbContext roleDbContext,
        CancellationToken cancellationToken)
    {
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize switch
        {
            < 1 => 25,
            > 200 => 200,
            _ => query.PageSize
        };

        var permissionsQuery = roleDbContext.Permissions.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = CreateSearchPattern(query.Search);
            permissionsQuery = permissionsQuery.Where(permission =>
                EF.Functions.ILike(permission.Name, pattern) ||
                EF.Functions.ILike(permission.Description ?? string.Empty, pattern));
        }

        var total = await permissionsQuery.CountAsync(cancellationToken).ConfigureAwait(false);

        var orderedQuery = query.Sort switch
        {
            "name:desc" => permissionsQuery
                .OrderByDescending(permission => permission.Name)
                .ThenBy(permission => permission.Id),
            "roleCount:desc" or "usage:desc" => permissionsQuery
                .OrderByDescending(permission => permission.RolePermissions.Count)
                .ThenBy(permission => permission.Name),
            "roleCount" or "roleCount:asc" or "usage" or "usage:asc" => permissionsQuery
                .OrderBy(permission => permission.RolePermissions.Count)
                .ThenBy(permission => permission.Name),
            _ => permissionsQuery
                .OrderBy(permission => permission.Name)
                .ThenBy(permission => permission.Id)
        };

        var skip = (page - 1) * pageSize;

        var permissions = await orderedQuery
            .Skip(skip)
            .Take(pageSize)
            .Select(permission => new
            {
                permission.Id,
                permission.Name,
                permission.Description,
                RoleCount = permission.RolePermissions.Count
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var items = permissions.Select(permission => new AdminPermissionSummary(
            permission.Id,
            permission.Name,
            permission.Description,
            permission.RoleCount)).ToList();

        return Results.Ok(new AdminPermissionListResponse(page, pageSize, total, items));
    }

    private static string CreateSearchPattern(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.Length == 0)
        {
            return "%";
        }

        var escaped = trimmed
            .Replace(@"\", @"\\", StringComparison.Ordinal)
            .Replace("%", @"\%", StringComparison.Ordinal)
            .Replace("_", @"\_", StringComparison.Ordinal);

        return $"%{escaped}%";
    }
}

internal sealed record AdminPermissionListQuery
{
    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 25;

    public string? Search { get; init; }

    public string? Sort { get; init; }
}

internal sealed record AdminPermissionSummary(
    Guid Id,
    string Name,
    string? Description,
    int RoleCount);

internal sealed record AdminPermissionListResponse(
    int Page,
    int PageSize,
    int TotalCount,
    IReadOnlyList<AdminPermissionSummary> Permissions);
