using System;
using System.Linq;
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

public static class OrganizationEndpoints
{
    public static IEndpointRouteBuilder MapOrganizationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapGet("/organizations", async (Guid? tenantId, IOrganizationService service, CancellationToken cancellationToken) =>
        {
            var organizations = await service.ListAsync(tenantId, cancellationToken).ConfigureAwait(false);
            return Results.Ok(organizations.Select(OrganizationApiMapper.ToOrganizationDto));
        })
        .RequireAuthorization(policy => policy.RequireOrganizationPermission(AdminOrganizationPermissions.OrganizationsRead));

        endpoints.MapPost("/organizations", async (CreateOrganizationRequest request, IValidator<CreateOrganizationRequest> validator, IOrganizationService service, CancellationToken cancellationToken) =>
        {
            var validationResult = await validator.ValidateAsync(request, cancellationToken).ConfigureAwait(false);
            if (!validationResult.IsValid)
            {
                return Results.ValidationProblem(validationResult.ToDictionary());
            }

            try
            {
                var organization = await service.CreateAsync(new OrganizationCreateRequest
                {
                    TenantId = request.TenantId,
                    Slug = request.Slug,
                    DisplayName = request.DisplayName,
                    Metadata = request.Metadata
                }, cancellationToken).ConfigureAwait(false);

                return Results.Created($"/organizations/{organization.Id}", OrganizationApiMapper.ToOrganizationDto(organization));
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new ProblemDetails { Title = "Invalid organization request", Detail = ex.Message, Status = StatusCodes.Status400BadRequest });
            }
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(new ProblemDetails { Title = "Organization conflict", Detail = ex.Message, Status = StatusCodes.Status409Conflict });
            }
        })
        .RequireAuthorization(policy => policy.RequireOrganizationPermission(AdminOrganizationPermissions.OrganizationsManage));

        endpoints.MapGet("/organizations/{organizationId:guid}", async (Guid organizationId, IOrganizationService service, CancellationToken cancellationToken) =>
        {
            var organization = await service.GetByIdAsync(organizationId, cancellationToken).ConfigureAwait(false);
            return organization is null ? Results.NotFound() : Results.Ok(OrganizationApiMapper.ToOrganizationDto(organization));
        })
        .RequireAuthorization(policy => policy.RequireOrganizationPermission(AdminOrganizationPermissions.OrganizationsRead));

        endpoints.MapPatch("/organizations/{organizationId:guid}", async (Guid organizationId, UpdateOrganizationRequest request, IValidator<UpdateOrganizationRequest> validator, IOrganizationService service, CancellationToken cancellationToken) =>
        {
            var validationResult = await validator.ValidateAsync(request, cancellationToken).ConfigureAwait(false);
            if (!validationResult.IsValid)
            {
                return Results.ValidationProblem(validationResult.ToDictionary());
            }

            try
            {
                var updated = await service.UpdateAsync(organizationId, new OrganizationUpdateRequest
                {
                    DisplayName = request.DisplayName,
                    Metadata = request.Metadata,
                    Status = request.Status
                }, cancellationToken).ConfigureAwait(false);

                return Results.Ok(OrganizationApiMapper.ToOrganizationDto(updated));
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound();
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new ProblemDetails { Title = "Invalid organization update", Detail = ex.Message, Status = StatusCodes.Status400BadRequest });
            }
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(new ProblemDetails { Title = "Organization conflict", Detail = ex.Message, Status = StatusCodes.Status409Conflict });
            }
        })
        .RequireAuthorization(policy => policy.RequireOrganizationPermission(AdminOrganizationPermissions.OrganizationsManage));

        endpoints.MapDelete("/organizations/{organizationId:guid}", async (Guid organizationId, IOrganizationService service, CancellationToken cancellationToken) =>
        {
            try
            {
                await service.ArchiveAsync(organizationId, cancellationToken).ConfigureAwait(false);
                return Results.NoContent();
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound();
            }
        })
        .RequireAuthorization(policy => policy.RequireOrganizationPermission(AdminOrganizationPermissions.OrganizationsManage));

        return endpoints;
    }
}
