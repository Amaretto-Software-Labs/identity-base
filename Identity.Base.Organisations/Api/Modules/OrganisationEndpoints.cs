using System;
using System.Linq;
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

public static class OrganisationEndpoints
{
    public static IEndpointRouteBuilder MapOrganisationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapGet("/organisations", async (Guid? tenantId, IOrganisationService service, CancellationToken cancellationToken) =>
        {
            var organisations = await service.ListAsync(tenantId, cancellationToken).ConfigureAwait(false);
            return Results.Ok(organisations.Select(OrganisationApiMapper.ToOrganisationDto));
        })
        .RequireAuthorization(policy => policy.RequireOrganisationPermission("organisations.read"));

        endpoints.MapPost("/organisations", async (CreateOrganisationRequest request, IValidator<CreateOrganisationRequest> validator, IOrganisationService service, CancellationToken cancellationToken) =>
        {
            var validationResult = await validator.ValidateAsync(request, cancellationToken).ConfigureAwait(false);
            if (!validationResult.IsValid)
            {
                return Results.ValidationProblem(validationResult.ToDictionary());
            }

            try
            {
                var organisation = await service.CreateAsync(new OrganisationCreateRequest
                {
                    TenantId = request.TenantId,
                    Slug = request.Slug,
                    DisplayName = request.DisplayName,
                    Metadata = request.Metadata
                }, cancellationToken).ConfigureAwait(false);

                return Results.Created($"/organisations/{organisation.Id}", OrganisationApiMapper.ToOrganisationDto(organisation));
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new ProblemDetails { Title = "Invalid organisation request", Detail = ex.Message, Status = StatusCodes.Status400BadRequest });
            }
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(new ProblemDetails { Title = "Organisation conflict", Detail = ex.Message, Status = StatusCodes.Status409Conflict });
            }
        })
        .RequireAuthorization(policy => policy.RequireOrganisationPermission("organisations.manage"));

        endpoints.MapGet("/organisations/{organisationId:guid}", async (Guid organisationId, IOrganisationService service, CancellationToken cancellationToken) =>
        {
            var organisation = await service.GetByIdAsync(organisationId, cancellationToken).ConfigureAwait(false);
            return organisation is null ? Results.NotFound() : Results.Ok(OrganisationApiMapper.ToOrganisationDto(organisation));
        })
        .RequireAuthorization(policy => policy.RequireOrganisationPermission("organisations.read"));

        endpoints.MapPatch("/organisations/{organisationId:guid}", async (Guid organisationId, UpdateOrganisationRequest request, IValidator<UpdateOrganisationRequest> validator, IOrganisationService service, CancellationToken cancellationToken) =>
        {
            var validationResult = await validator.ValidateAsync(request, cancellationToken).ConfigureAwait(false);
            if (!validationResult.IsValid)
            {
                return Results.ValidationProblem(validationResult.ToDictionary());
            }

            try
            {
                var updated = await service.UpdateAsync(organisationId, new OrganisationUpdateRequest
                {
                    DisplayName = request.DisplayName,
                    Metadata = request.Metadata,
                    Status = request.Status
                }, cancellationToken).ConfigureAwait(false);

                return Results.Ok(OrganisationApiMapper.ToOrganisationDto(updated));
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound();
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new ProblemDetails { Title = "Invalid organisation update", Detail = ex.Message, Status = StatusCodes.Status400BadRequest });
            }
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(new ProblemDetails { Title = "Organisation conflict", Detail = ex.Message, Status = StatusCodes.Status409Conflict });
            }
        })
        .RequireAuthorization(policy => policy.RequireOrganisationPermission("organisations.manage"));

        endpoints.MapDelete("/organisations/{organisationId:guid}", async (Guid organisationId, IOrganisationService service, CancellationToken cancellationToken) =>
        {
            try
            {
                await service.ArchiveAsync(organisationId, cancellationToken).ConfigureAwait(false);
                return Results.NoContent();
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound();
            }
        })
        .RequireAuthorization(policy => policy.RequireOrganisationPermission("organisations.manage"));

        return endpoints;
    }
}
