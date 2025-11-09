using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Identity.Base.Abstractions.Pagination;
using Identity.Base.Admin.Authorization;
using Identity.Base.Admin.Configuration;
using Identity.Base.Extensions;
using Identity.Base.Roles.Abstractions;
using Identity.Base.Roles.Entities;
using Identity.Base.Admin.Diagnostics;
using Identity.Base.Admin.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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
            .Produces<PagedResult<AdminPermissionSummary>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .RequireAuthorization(policy => policy.RequireAdminPermission("roles.read"));

        return group;
    }

    private static async Task<IResult> ListPermissionsAsync(
        [AsParameters] AdminPermissionListQuery query,
        IRoleDbContext roleDbContext,
        ILoggerFactory loggerFactory,
        IOptions<AdminDiagnosticsOptions> diagnosticsOptions,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger(typeof(AdminPermissionEndpoints).FullName!);
        var slowQueryThresholdMs = Math.Max(0, diagnosticsOptions.Value.SlowQueryThreshold.TotalMilliseconds);
        var stopwatch = Stopwatch.StartNew();

        var pageRequest = PageRequest.Create(
            query.Page,
            query.PageSize,
            query.Search,
            query.Sort is null ? null : new[] { query.Sort },
            defaultPageSize: 25,
            maxPageSize: 200);

        var permissionsQuery = roleDbContext.Permissions.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(pageRequest.Search))
        {
            var pattern = SearchPatternHelper.CreateSearchPattern(pageRequest.Search).ToUpperInvariant();
            permissionsQuery = permissionsQuery.Where(permission =>
                EF.Functions.Like((permission.Name ?? string.Empty).ToUpper(), pattern) ||
                EF.Functions.Like((permission.Description ?? string.Empty).ToUpper(), pattern));
        }

        var total = await permissionsQuery.CountAsync(cancellationToken).ConfigureAwait(false);

        if (total == 0)
        {
            stopwatch.Stop();
            var tagsEmpty = AdminMetrics.BuildPermissionQueryTags(query);
            AdminMetrics.PermissionsListDuration.Record(stopwatch.Elapsed.TotalMilliseconds, tagsEmpty);
            AdminMetrics.PermissionsListResultCount.Record(0, tagsEmpty);

            logger.LogInformation(
                "Listed admin permissions in {ElapsedMs} ms (page {Page}/{PageSize}, total 0, returned 0, search={HasSearch}, sort={Sort})",
                stopwatch.Elapsed.TotalMilliseconds,
                pageRequest.Page,
                pageRequest.PageSize,
                string.IsNullOrWhiteSpace(pageRequest.Search) ? "false" : "true",
                FormatSorts(query.Sort, "name"));

            return Results.Ok(PagedResult<AdminPermissionSummary>.Empty(pageRequest.Page, pageRequest.PageSize));
        }

        var orderedQuery = ApplyPermissionSorting(permissionsQuery, pageRequest);

        var skip = pageRequest.GetSkip();

        var permissions = await orderedQuery
            .Skip(skip)
            .Take(pageRequest.PageSize)
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

        stopwatch.Stop();
        var elapsedMs = stopwatch.Elapsed.TotalMilliseconds;

        var tags = AdminMetrics.BuildPermissionQueryTags(query);
        AdminMetrics.PermissionsListDuration.Record(elapsedMs, tags);
        AdminMetrics.PermissionsListResultCount.Record(items.Count, tags);

        logger.LogInformation(
            "Listed admin permissions in {ElapsedMs} ms (page {Page}/{PageSize}, total {TotalCount}, returned {ReturnedCount}, search={HasSearch}, sort={Sort})",
            elapsedMs,
            pageRequest.Page,
            pageRequest.PageSize,
            total,
            items.Count,
            string.IsNullOrWhiteSpace(pageRequest.Search) ? "false" : "true",
            FormatSorts(query.Sort, "name"));

        if (slowQueryThresholdMs > 0 && elapsedMs > slowQueryThresholdMs)
        {
            logger.LogWarning(
                "Admin permissions list query exceeded threshold: {ElapsedMs} ms > {Threshold} ms (page {Page}/{PageSize})",
                elapsedMs,
                slowQueryThresholdMs,
                pageRequest.Page,
                pageRequest.PageSize);
        }

        return Results.Ok(new PagedResult<AdminPermissionSummary>(pageRequest.Page, pageRequest.PageSize, total, items));
    }

    private static IOrderedQueryable<Permission> ApplyPermissionSorting(IQueryable<Permission> source, PageRequest request)
    {
        IOrderedQueryable<Permission>? ordered = null;

        if (request.Sorts.Count > 0)
        {
            foreach (var sort in request.Sorts)
            {
                var key = sort.Field.ToLowerInvariant();
                ordered = key switch
                {
                    "name" => ApplyPermissionOrder(source, ordered, permission => permission.Name, sort.Direction),
                    "rolecount" or "usage" => ApplyPermissionOrder(source, ordered, permission => permission.RolePermissions.Count, sort.Direction),
                    _ => ordered
                };

                if (ordered is not null)
                {
                    source = ordered;
                }
            }
        }

        ordered ??= source.OrderBy(permission => permission.Name);
        return ordered.ThenBy(permission => permission.Id);
    }

    private static IOrderedQueryable<Permission> ApplyPermissionOrder<T>(
        IQueryable<Permission> source,
        IOrderedQueryable<Permission>? ordered,
        Expression<Func<Permission, T>> keySelector,
        SortDirection direction)
    {
        if (ordered is null)
        {
            return direction == SortDirection.Descending
                ? source.OrderByDescending(keySelector)
                : source.OrderBy(keySelector);
        }

        return direction == SortDirection.Descending
            ? ordered.ThenByDescending(keySelector)
            : ordered.ThenBy(keySelector);
    }

    private static string FormatSorts(string? sort, string defaultValue)
        => string.IsNullOrWhiteSpace(sort)
            ? defaultValue
            : sort;
}

internal sealed class AdminPermissionListQuery
{
    public int? Page { get; set; }

    public int? PageSize { get; set; }

    public string? Search { get; set; }

    public string? Sort { get; set; }
}

internal sealed record AdminPermissionSummary(
    Guid Id,
    string Name,
    string? Description,
    int RoleCount);
