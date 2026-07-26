using Identity.Base.Abstractions.Pagination;
using Identity.Base.Admin.Configuration;
using Identity.Base.Logging;
using Identity.Base.Roles.Abstractions;
using Identity.Base.ServicePrincipals.Data;
using Identity.Base.ServicePrincipals.Options;
using Identity.Base.ServicePrincipals.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Identity.Base.ServicePrincipals.Api;

internal static class ServicePrincipalEndpoints
{
    public static RouteGroupBuilder MapServicePrincipalEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/admin/service-principals").WithTags("Admin.ServicePrincipals");
        group.MapGet("", ListAsync).RequireAuthorization(p => p.RequireAdminPermission(ServicePrincipalPermissions.Read));
        group.MapGet("/{id:guid}", GetAsync).RequireAuthorization(p => p.RequireAdminPermission(ServicePrincipalPermissions.Read));
        group.MapPost("", CreateAsync).RequireAuthorization(p => p.RequireAdminPermission(ServicePrincipalPermissions.Create));
        group.MapPut("/{id:guid}", UpdateAsync).RequireAuthorization(p => p.RequireAdminPermission(ServicePrincipalPermissions.Update));
        group.MapPost("/{id:guid}/disable", DisableAsync).RequireAuthorization(p => p.RequireAdminPermission(ServicePrincipalPermissions.Disable));
        group.MapPost("/{id:guid}/restore", RestoreAsync).RequireAuthorization(p => p.RequireAdminPermission(ServicePrincipalPermissions.Disable));
        group.MapGet("/{id:guid}/roles", GetRolesAsync).RequireAuthorization(p => p.RequireAdminPermission(ServicePrincipalPermissions.Read));
        group.MapPut("/{id:guid}/roles", PutRolesAsync).RequireAuthorization(p => p.RequireAdminPermission(ServicePrincipalPermissions.ManageRoles));
        group.MapGet("/{id:guid}/credentials", ListCredentialsAsync).RequireAuthorization(p => p.RequireAdminPermission(ServicePrincipalPermissions.Read));
        group.MapPost("/{id:guid}/credentials", IssueCredentialAsync).RequireAuthorization(p => p.RequireAdminPermission(ServicePrincipalPermissions.ManageCredentials));
        group.MapPost("/{id:guid}/credentials/{credentialId:guid}/revoke", RevokeCredentialAsync).RequireAuthorization(p => p.RequireAdminPermission(ServicePrincipalPermissions.ManageCredentials));
        group.MapPost("/{id:guid}/credentials/revoke-all", RevokeAllCredentialsAsync).RequireAuthorization(p => p.RequireAdminPermission(ServicePrincipalPermissions.ManageCredentials));
        return group;
    }

    private static async Task<IResult> ListAsync(
        [AsParameters] ListQuery query,
        ServicePrincipalDbContext dbContext,
        IRoleDbContext roleDbContext,
        CancellationToken cancellationToken)
    {
        var page = Math.Max(1, query.Page ?? 1);
        var pageSize = Math.Clamp(query.PageSize ?? 25, 1, 100);
        var source = dbContext.ServicePrincipals.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            source = source.Where(item => item.DisplayName.Contains(search) || item.ClientId.Contains(search));
        }
        if (query.Disabled.HasValue)
        {
            source = source.Where(item => item.IsDisabled == query.Disabled.Value);
        }

        var total = await source.CountAsync(cancellationToken);
        var principals = await source.OrderBy(item => item.DisplayName).ThenBy(item => item.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(cancellationToken);
        var principalIds = principals.Select(item => item.Id).ToArray();
        var roleAssignments = await roleDbContext.ServicePrincipalRoles
            .Where(item => principalIds.Contains(item.ServicePrincipalId))
            .Join(
                roleDbContext.Roles,
                assignment => assignment.RoleId,
                role => role.Id,
                (assignment, role) => new { assignment.ServicePrincipalId, role.Name })
            .ToListAsync(cancellationToken);
        var rolesByPrincipal = roleAssignments.ToLookup(item => item.ServicePrincipalId, item => item.Name);
        var items = principals.Select(item => new ServicePrincipalSummary(
                item.Id, item.DisplayName, item.ClientId, item.IsDisabled,
                item.CreatedAt, item.UpdatedAt, item.ConcurrencyStamp,
                rolesByPrincipal[item.Id].OrderBy(name => name).ToArray()))
            .ToArray();
        return Results.Ok(new PagedResult<ServicePrincipalSummary>(page, pageSize, total, items));
    }

    private static async Task<IResult> GetAsync(
        Guid id,
        ServicePrincipalDbContext dbContext,
        IRoleDbContext roleDbContext,
        CancellationToken cancellationToken)
    {
        var principal = await dbContext.ServicePrincipals.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (principal is null)
        {
            return Results.NotFound();
        }
        var roles = await GetRoleNamesAsync(id, roleDbContext, cancellationToken);
        return Results.Ok(new ServicePrincipalDetail(
            principal.Id, principal.DisplayName, principal.ClientId, principal.IsDisabled,
            principal.CreatedAt, principal.UpdatedAt, principal.ConcurrencyStamp, roles));
    }

    private static async Task<IResult> CreateAsync(
        CreateServicePrincipalRequest request,
        ServicePrincipalService service,
        IAuditLogger auditLogger,
        CancellationToken cancellationToken)
    {
        try
        {
            var principal = await service.CreateAsync(request.DisplayName, cancellationToken);
            await auditLogger.LogAnonymousAsync(AuditEventTypes.AdminServicePrincipalCreated,
                new { principal.Id, principal.ClientId }, cancellationToken);
            return Results.Created($"/admin/service-principals/{principal.Id:D}", new ServicePrincipalSummary(
                principal.Id, principal.DisplayName, principal.ClientId, principal.IsDisabled,
                principal.CreatedAt, principal.UpdatedAt, principal.ConcurrencyStamp, []));
        }
        catch (ArgumentException exception)
        {
            return ArgumentValidationProblem(exception);
        }
        catch (InvalidOperationException exception)
        {
            return Results.Conflict(new ProblemDetails { Detail = exception.Message, Status = StatusCodes.Status409Conflict });
        }
    }

    private static async Task<IResult> UpdateAsync(
        Guid id,
        UpdateServicePrincipalRequest request,
        ServicePrincipalService service,
        ServicePrincipalDbContext dbContext,
        IRoleDbContext roleDbContext,
        IAuditLogger auditLogger,
        CancellationToken cancellationToken)
    {
        try
        {
            var principal = await service.FindRequiredAsync(id, cancellationToken);
            if (!string.Equals(principal.ConcurrencyStamp, request.ConcurrencyStamp, StringComparison.Ordinal))
            {
                return ConcurrentModificationConflict();
            }
            principal.UpdateDisplayName(request.DisplayName);
            await dbContext.SaveChangesAsync(cancellationToken);
            await auditLogger.LogAnonymousAsync(AuditEventTypes.AdminServicePrincipalUpdated,
                new { principal.Id, principal.ClientId }, cancellationToken);
            var roles = await GetRoleNamesAsync(id, roleDbContext, cancellationToken);
            return Results.Ok(new ServicePrincipalSummary(
                principal.Id, principal.DisplayName, principal.ClientId, principal.IsDisabled,
                principal.CreatedAt, principal.UpdatedAt, principal.ConcurrencyStamp, roles));
        }
        catch (KeyNotFoundException)
        {
            return Results.NotFound();
        }
        catch (ArgumentException exception)
        {
            return ArgumentValidationProblem(exception);
        }
        catch (DbUpdateConcurrencyException)
        {
            return ConcurrentModificationConflict();
        }
    }

    private static IResult ConcurrentModificationConflict() =>
        Results.Conflict(new ProblemDetails
        {
            Detail = "Service principal was modified by another process.",
            Status = StatusCodes.Status409Conflict
        });

    private static async Task<IResult> DisableAsync(
        Guid id, RevokeServicePrincipalCredentialRequest? request, ServicePrincipalService service,
        IAuditLogger auditLogger, CancellationToken cancellationToken)
    {
        try
        {
            await service.DisableAsync(id, request?.Reason, cancellationToken);
            await auditLogger.LogAnonymousAsync(AuditEventTypes.AdminServicePrincipalDisabled, new { Id = id }, cancellationToken);
            return Results.NoContent();
        }
        catch (KeyNotFoundException) { return Results.NotFound(); }
        catch (ArgumentException exception) { return ArgumentValidationProblem(exception); }
        catch (InvalidOperationException exception)
        {
            return Results.Conflict(new ProblemDetails
            {
                Detail = exception.Message,
                Status = StatusCodes.Status409Conflict
            });
        }
    }

    private static async Task<IResult> RestoreAsync(
        Guid id, ServicePrincipalService service, IAuditLogger auditLogger, CancellationToken cancellationToken)
    {
        try
        {
            await service.RestoreAsync(id, cancellationToken);
            await auditLogger.LogAnonymousAsync(AuditEventTypes.AdminServicePrincipalRestored, new { Id = id }, cancellationToken);
            return Results.NoContent();
        }
        catch (KeyNotFoundException) { return Results.NotFound(); }
    }

    private static async Task<IResult> GetRolesAsync(
        Guid id, ServicePrincipalDbContext dbContext, IRoleDbContext roleDbContext, CancellationToken cancellationToken)
    {
        if (!await dbContext.ServicePrincipals.AnyAsync(item => item.Id == id, cancellationToken))
        {
            return Results.NotFound();
        }
        return Results.Ok(new ServicePrincipalRolesResponse(await GetRoleNamesAsync(id, roleDbContext, cancellationToken)));
    }

    private static async Task<IResult> PutRolesAsync(
        Guid id, UpdateServicePrincipalRolesRequest request, ServicePrincipalService service,
        IRoleDbContext roleDbContext, IAuditLogger auditLogger, CancellationToken cancellationToken)
    {
        try
        {
            await service.ReplaceRolesAsync(id, request.Roles ?? [], cancellationToken);
            var roles = await GetRoleNamesAsync(id, roleDbContext, cancellationToken);
            await auditLogger.LogAnonymousAsync(AuditEventTypes.AdminServicePrincipalRolesUpdated,
                new { Id = id, Roles = roles }, cancellationToken);
            return Results.Ok(new ServicePrincipalRolesResponse(roles));
        }
        catch (KeyNotFoundException) { return Results.NotFound(); }
        catch (InvalidOperationException exception)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["roles"] = [exception.Message] });
        }
    }

    private static async Task<IResult> ListCredentialsAsync(
        Guid id, ServicePrincipalDbContext dbContext, CancellationToken cancellationToken)
    {
        if (!await dbContext.ServicePrincipals.AnyAsync(item => item.Id == id, cancellationToken))
        {
            return Results.NotFound();
        }
        var credentials = await dbContext.ServicePrincipalCredentials.AsNoTracking()
            .Where(item => item.ServicePrincipalId == id).OrderBy(item => item.Name)
            .Select(item => new ServicePrincipalCredentialSummary(
                item.Id, item.Name, item.CreatedAt, item.ExpiresAt, item.RevokedAt, item.RevokedReason))
            .ToListAsync(cancellationToken);
        return Results.Ok(credentials);
    }

    private static async Task<IResult> IssueCredentialAsync(
        Guid id, IssueServicePrincipalCredentialRequest request, ServicePrincipalService service,
        IAuditLogger auditLogger, CancellationToken cancellationToken)
    {
        try
        {
            var issued = await service.IssueCredentialAsync(id, request.Name, request.ExpiresAt, cancellationToken);
            await auditLogger.LogAnonymousAsync(AuditEventTypes.AdminServicePrincipalCredentialIssued,
                new { ServicePrincipalId = id, issued.Credential.Id, issued.Credential.Name }, cancellationToken);
            return Results.Created($"/admin/service-principals/{id:D}/credentials/{issued.Credential.Id:D}",
                new IssuedServicePrincipalCredential(issued.Credential.Id, issued.Credential.Name, issued.Secret,
                    issued.Credential.CreatedAt, issued.Credential.ExpiresAt));
        }
        catch (KeyNotFoundException) { return Results.NotFound(); }
        catch (ArgumentException exception) { return ArgumentValidationProblem(exception); }
        catch (InvalidOperationException exception)
        {
            return Results.Conflict(new ProblemDetails
            {
                Detail = exception.Message,
                Status = StatusCodes.Status409Conflict
            });
        }
    }

    private static async Task<IResult> RevokeCredentialAsync(
        Guid id, Guid credentialId, RevokeServicePrincipalCredentialRequest? request,
        ServicePrincipalService service, IAuditLogger auditLogger, CancellationToken cancellationToken)
    {
        try
        {
            await service.RevokeCredentialAsync(id, credentialId, request?.Reason, cancellationToken);
            await auditLogger.LogAnonymousAsync(AuditEventTypes.AdminServicePrincipalCredentialRevoked,
                new { ServicePrincipalId = id, CredentialId = credentialId }, cancellationToken);
            return Results.NoContent();
        }
        catch (KeyNotFoundException) { return Results.NotFound(); }
        catch (ArgumentException exception) { return ArgumentValidationProblem(exception); }
    }

    private static async Task<IResult> RevokeAllCredentialsAsync(
        Guid id, RevokeServicePrincipalCredentialRequest? request, ServicePrincipalService service,
        IAuditLogger auditLogger, CancellationToken cancellationToken)
    {
        try
        {
            await service.RevokeAllCredentialsAsync(id, request?.Reason, cancellationToken);
            await auditLogger.LogAnonymousAsync(AuditEventTypes.AdminServicePrincipalCredentialsRevoked,
                new { ServicePrincipalId = id }, cancellationToken);
            return Results.NoContent();
        }
        catch (KeyNotFoundException) { return Results.NotFound(); }
        catch (ArgumentException exception) { return ArgumentValidationProblem(exception); }
    }

    private static IResult ArgumentValidationProblem(ArgumentException exception) =>
        Results.ValidationProblem(new Dictionary<string, string[]>
        {
            [exception.ParamName ?? "request"] = [exception.Message]
        });

    private static Task<List<string>> GetRoleNamesAsync(
        Guid id, IRoleDbContext roleDbContext, CancellationToken cancellationToken) =>
        roleDbContext.ServicePrincipalRoles.Where(item => item.ServicePrincipalId == id)
            .Join(roleDbContext.Roles, item => item.RoleId, role => role.Id, (_, role) => role.Name)
            .OrderBy(name => name).ToListAsync(cancellationToken);

    internal sealed class ListQuery
    {
        public int? Page { get; set; }
        public int? PageSize { get; set; }
        public string? Search { get; set; }
        public bool? Disabled { get; set; }
    }
}
