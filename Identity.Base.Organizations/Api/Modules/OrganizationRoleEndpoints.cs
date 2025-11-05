using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using FluentValidation;
using Identity.Base.Extensions;
using Identity.Base.Organizations.Abstractions;
using Identity.Base.Organizations.Api.Models;
using Identity.Base.Organizations.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Identity.Base.Organizations.Api.Modules;

public static class OrganizationRoleEndpoints
{
    public static IEndpointRouteBuilder MapOrganizationRoleEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapGet("/organizations/{organizationId:guid}/roles", async (Guid organizationId, Guid? tenantId, ClaimsPrincipal principal, IOrganizationScopeResolver scopeResolver, IOrganizationRoleService roleService, CancellationToken cancellationToken) =>
        {
            var scopeResult = await EnsureActorInScopeAsync(principal, scopeResolver, organizationId, cancellationToken).ConfigureAwait(false);
            if (scopeResult is not null)
            {
                return scopeResult;
            }

            var roles = await roleService.ListAsync(tenantId, organizationId, cancellationToken).ConfigureAwait(false);
            return Results.Ok(roles.Select(OrganizationApiMapper.ToRoleDto));
        })
        .RequireAuthorization(policy => policy.RequireOrganizationPermission(AdminOrganizationPermissions.OrganizationRolesRead));

        endpoints.MapPost("/organizations/{organizationId:guid}/roles", async (Guid organizationId, CreateOrganizationRoleRequest request, IValidator<CreateOrganizationRoleRequest> validator, ClaimsPrincipal principal, IOrganizationScopeResolver scopeResolver, IOrganizationRoleService roleService, CancellationToken cancellationToken) =>
        {
            var validationResult = await validator.ValidateAsync(request, cancellationToken).ConfigureAwait(false);
            if (!validationResult.IsValid)
            {
                return Results.ValidationProblem(validationResult.ToDictionary());
            }

            var scopeResult = await EnsureActorInScopeAsync(principal, scopeResolver, organizationId, cancellationToken).ConfigureAwait(false);
            if (scopeResult is not null)
            {
                return scopeResult;
            }

            try
            {
                var role = await roleService.CreateAsync(new OrganizationRoleCreateRequest
                {
                    OrganizationId = organizationId,
                    TenantId = null,
                    Name = request.Name,
                    Description = request.Description,
                    IsSystemRole = request.IsSystemRole
                }, cancellationToken).ConfigureAwait(false);

                return Results.Created($"/organizations/{organizationId}/roles/{role.Id}", OrganizationApiMapper.ToRoleDto(role));
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new ProblemDetails { Title = "Invalid role", Detail = ex.Message, Status = StatusCodes.Status400BadRequest });
            }
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(new ProblemDetails { Title = "Role conflict", Detail = ex.Message, Status = StatusCodes.Status409Conflict });
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new ProblemDetails { Title = "Organization not found", Detail = ex.Message, Status = StatusCodes.Status404NotFound });
            }
        })
        .RequireAuthorization(policy => policy.RequireOrganizationPermission(AdminOrganizationPermissions.OrganizationRolesManage));

        endpoints.MapDelete("/organizations/{organizationId:guid}/roles/{roleId:guid}", async (Guid organizationId, Guid roleId, ClaimsPrincipal principal, IOrganizationScopeResolver scopeResolver, IOrganizationRoleService roleService, CancellationToken cancellationToken) =>
        {
            var scopeResult = await EnsureActorInScopeAsync(principal, scopeResolver, organizationId, cancellationToken).ConfigureAwait(false);
            if (scopeResult is not null)
            {
                return scopeResult;
            }

            try
            {
                await roleService.DeleteAsync(roleId, cancellationToken).ConfigureAwait(false);
                return Results.NoContent();
            }
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(new ProblemDetails { Title = "Role conflict", Detail = ex.Message, Status = StatusCodes.Status409Conflict });
            }
        })
        .RequireAuthorization(policy => policy.RequireOrganizationPermission(AdminOrganizationPermissions.OrganizationRolesManage));

        endpoints.MapGet("/organizations/{organizationId:guid}/roles/{roleId:guid}/permissions", async (Guid organizationId, Guid roleId, ClaimsPrincipal principal, IOrganizationScopeResolver scopeResolver, IOrganizationRoleService roleService, CancellationToken cancellationToken) =>
        {
            var scopeResult = await EnsureActorInScopeAsync(principal, scopeResolver, organizationId, cancellationToken).ConfigureAwait(false);
            if (scopeResult is not null)
            {
                return scopeResult;
            }

            try
            {
                var permissionSet = await roleService.GetPermissionsAsync(roleId, organizationId, cancellationToken).ConfigureAwait(false);
                return Results.Ok(OrganizationApiMapper.ToRolePermissionsResponse(permissionSet));
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new ProblemDetails { Title = "Role not found", Detail = ex.Message, Status = StatusCodes.Status404NotFound });
            }
        })
        .RequireAuthorization(policy => policy.RequireOrganizationPermission(AdminOrganizationPermissions.OrganizationRolesRead));

        endpoints.MapPut("/organizations/{organizationId:guid}/roles/{roleId:guid}/permissions", async (Guid organizationId, Guid roleId, UpdateOrganizationRolePermissionsRequest request, IValidator<UpdateOrganizationRolePermissionsRequest> validator, ClaimsPrincipal principal, IOrganizationScopeResolver scopeResolver, IOrganizationRoleService roleService, CancellationToken cancellationToken) =>
        {
            var validationResult = await validator.ValidateAsync(request, cancellationToken).ConfigureAwait(false);
            if (!validationResult.IsValid)
            {
                return Results.ValidationProblem(validationResult.ToDictionary());
            }

            var scopeResult = await EnsureActorInScopeAsync(principal, scopeResolver, organizationId, cancellationToken).ConfigureAwait(false);
            if (scopeResult is not null)
            {
                return scopeResult;
            }

            try
            {
                await roleService.UpdatePermissionsAsync(roleId, organizationId, request.Permissions, cancellationToken).ConfigureAwait(false);
                return Results.NoContent();
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new ProblemDetails { Title = "Resource not found", Detail = ex.Message, Status = StatusCodes.Status404NotFound });
            }
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(new ProblemDetails { Title = "Role conflict", Detail = ex.Message, Status = StatusCodes.Status409Conflict });
            }
        })
        .RequireAuthorization(policy => policy.RequireOrganizationPermission(AdminOrganizationPermissions.OrganizationRolesManage));

        return endpoints;
    }

    private static async Task<IResult?> EnsureActorInScopeAsync(
        ClaimsPrincipal principal,
        IOrganizationScopeResolver scopeResolver,
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(principal, out var actorUserId))
        {
            return Results.Unauthorized();
        }

        if (organizationId == Guid.Empty)
        {
            return Results.BadRequest(new ProblemDetails { Title = "Invalid organization", Detail = "Organization identifier is required.", Status = StatusCodes.Status400BadRequest });
        }

        var inScope = await scopeResolver.IsInScopeAsync(actorUserId, organizationId, cancellationToken).ConfigureAwait(false);
        if (!inScope)
        {
            return Results.Forbid();
        }

        return null;
    }

    private static bool TryGetUserId(ClaimsPrincipal principal, out Guid userId)
    {
        var value = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out userId);
    }
}
