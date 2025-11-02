using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using FluentValidation;
using Identity.Base.Extensions;
using Identity.Base.Organisations.Abstractions;
using Identity.Base.Organisations.Api.Models;
using Identity.Base.Organisations.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Identity.Base.Organisations.Api.Modules;

public static class OrganisationRoleEndpoints
{
    public static IEndpointRouteBuilder MapOrganisationRoleEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapGet("/organisations/{organisationId:guid}/roles", async (Guid organisationId, Guid? tenantId, ClaimsPrincipal principal, IOrganisationScopeResolver scopeResolver, IOrganisationRoleService roleService, CancellationToken cancellationToken) =>
        {
            var scopeResult = await EnsureActorInScopeAsync(principal, scopeResolver, organisationId, cancellationToken).ConfigureAwait(false);
            if (scopeResult is not null)
            {
                return scopeResult;
            }

            var roles = await roleService.ListAsync(tenantId, organisationId, cancellationToken).ConfigureAwait(false);
            return Results.Ok(roles.Select(OrganisationApiMapper.ToRoleDto));
        })
        .RequireAuthorization(policy => policy.RequireOrganisationPermission("organisation.roles.read"));

        endpoints.MapPost("/organisations/{organisationId:guid}/roles", async (Guid organisationId, CreateOrganisationRoleRequest request, IValidator<CreateOrganisationRoleRequest> validator, ClaimsPrincipal principal, IOrganisationScopeResolver scopeResolver, IOrganisationRoleService roleService, CancellationToken cancellationToken) =>
        {
            var validationResult = await validator.ValidateAsync(request, cancellationToken).ConfigureAwait(false);
            if (!validationResult.IsValid)
            {
                return Results.ValidationProblem(validationResult.ToDictionary());
            }

            var scopeResult = await EnsureActorInScopeAsync(principal, scopeResolver, organisationId, cancellationToken).ConfigureAwait(false);
            if (scopeResult is not null)
            {
                return scopeResult;
            }

            try
            {
                var role = await roleService.CreateAsync(new OrganisationRoleCreateRequest
                {
                    OrganisationId = organisationId,
                    TenantId = null,
                    Name = request.Name,
                    Description = request.Description,
                    IsSystemRole = request.IsSystemRole
                }, cancellationToken).ConfigureAwait(false);

                return Results.Created($"/organisations/{organisationId}/roles/{role.Id}", OrganisationApiMapper.ToRoleDto(role));
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
                return Results.NotFound(new ProblemDetails { Title = "Organisation not found", Detail = ex.Message, Status = StatusCodes.Status404NotFound });
            }
        })
        .RequireAuthorization(policy => policy.RequireOrganisationPermission("organisation.roles.manage"));

        endpoints.MapDelete("/organisations/{organisationId:guid}/roles/{roleId:guid}", async (Guid organisationId, Guid roleId, ClaimsPrincipal principal, IOrganisationScopeResolver scopeResolver, IOrganisationRoleService roleService, CancellationToken cancellationToken) =>
        {
            var scopeResult = await EnsureActorInScopeAsync(principal, scopeResolver, organisationId, cancellationToken).ConfigureAwait(false);
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
        .RequireAuthorization(policy => policy.RequireOrganisationPermission("organisation.roles.manage"));

        endpoints.MapGet("/organisations/{organisationId:guid}/roles/{roleId:guid}/permissions", async (Guid organisationId, Guid roleId, ClaimsPrincipal principal, IOrganisationScopeResolver scopeResolver, IOrganisationRoleService roleService, CancellationToken cancellationToken) =>
        {
            var scopeResult = await EnsureActorInScopeAsync(principal, scopeResolver, organisationId, cancellationToken).ConfigureAwait(false);
            if (scopeResult is not null)
            {
                return scopeResult;
            }

            try
            {
                var permissionSet = await roleService.GetPermissionsAsync(roleId, organisationId, cancellationToken).ConfigureAwait(false);
                return Results.Ok(OrganisationApiMapper.ToRolePermissionsResponse(permissionSet));
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new ProblemDetails { Title = "Role not found", Detail = ex.Message, Status = StatusCodes.Status404NotFound });
            }
        })
        .RequireAuthorization(policy => policy.RequireOrganisationPermission("organisation.roles.read"));

        endpoints.MapPut("/organisations/{organisationId:guid}/roles/{roleId:guid}/permissions", async (Guid organisationId, Guid roleId, UpdateOrganisationRolePermissionsRequest request, IValidator<UpdateOrganisationRolePermissionsRequest> validator, ClaimsPrincipal principal, IOrganisationScopeResolver scopeResolver, IOrganisationRoleService roleService, CancellationToken cancellationToken) =>
        {
            var validationResult = await validator.ValidateAsync(request, cancellationToken).ConfigureAwait(false);
            if (!validationResult.IsValid)
            {
                return Results.ValidationProblem(validationResult.ToDictionary());
            }

            var scopeResult = await EnsureActorInScopeAsync(principal, scopeResolver, organisationId, cancellationToken).ConfigureAwait(false);
            if (scopeResult is not null)
            {
                return scopeResult;
            }

            try
            {
                await roleService.UpdatePermissionsAsync(roleId, organisationId, request.Permissions, cancellationToken).ConfigureAwait(false);
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
        .RequireAuthorization(policy => policy.RequireOrganisationPermission("organisation.roles.manage"));

        return endpoints;
    }

    private static async Task<IResult?> EnsureActorInScopeAsync(
        ClaimsPrincipal principal,
        IOrganisationScopeResolver scopeResolver,
        Guid organisationId,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(principal, out var actorUserId))
        {
            return Results.Unauthorized();
        }

        if (organisationId == Guid.Empty)
        {
            return Results.BadRequest(new ProblemDetails { Title = "Invalid organisation", Detail = "Organisation identifier is required.", Status = StatusCodes.Status400BadRequest });
        }

        var inScope = await scopeResolver.IsInScopeAsync(actorUserId, organisationId, cancellationToken).ConfigureAwait(false);
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
