using System;
using System.Linq;
using System.Threading;
using FluentValidation;
using Identity.Base.Extensions;
using Identity.Base.Organizations.Abstractions;
using Identity.Base.Organizations.Api.Models;
using Identity.Base.Organizations.Domain;
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

        endpoints.MapGet("/organizations/{organizationId:guid}/roles", async (Guid organizationId, Guid? tenantId, IOrganizationRoleService roleService, CancellationToken cancellationToken) =>
        {
            var roles = await roleService.ListAsync(tenantId, organizationId, cancellationToken).ConfigureAwait(false);
            return Results.Ok(roles.Select(ToDto));
        });

        endpoints.MapPost("/organizations/{organizationId:guid}/roles", async (Guid organizationId, CreateOrganizationRoleRequest request, IValidator<CreateOrganizationRoleRequest> validator, IOrganizationRoleService roleService, CancellationToken cancellationToken) =>
        {
            var validationResult = await validator.ValidateAsync(request, cancellationToken).ConfigureAwait(false);
            if (!validationResult.IsValid)
            {
                return Results.ValidationProblem(validationResult.ToDictionary());
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

                return Results.Created($"/organizations/{organizationId}/roles/{role.Id}", ToDto(role));
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
        });

        endpoints.MapDelete("/organizations/{organizationId:guid}/roles/{roleId:guid}", async (Guid organizationId, Guid roleId, IOrganizationRoleService roleService, CancellationToken cancellationToken) =>
        {
            try
            {
                await roleService.DeleteAsync(roleId, cancellationToken).ConfigureAwait(false);
                return Results.NoContent();
            }
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(new ProblemDetails { Title = "Role conflict", Detail = ex.Message, Status = StatusCodes.Status409Conflict });
            }
        });

        return endpoints;
    }

    private static OrganizationRoleDto ToDto(OrganizationRole role)
        => new()
        {
            Id = role.Id,
            OrganizationId = role.OrganizationId,
            TenantId = role.TenantId,
            Name = role.Name,
            Description = role.Description,
            IsSystemRole = role.IsSystemRole,
            CreatedAtUtc = role.CreatedAtUtc,
            UpdatedAtUtc = role.UpdatedAtUtc
        };
}
