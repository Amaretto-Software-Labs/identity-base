using System;
using System.Linq;
using System.Security.Claims;
using FluentValidation;
using Identity.Base.Extensions;
using Identity.Base.Organisations.Abstractions;
using Identity.Base.Organisations.Api.Models;
using Identity.Base.Organisations.Domain;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Identity.Base.Organisations.Api.Modules;

public static class UserOrganisationEndpoints
{
    public static IEndpointRouteBuilder MapUserOrganisationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapGet("/users/me/organisations", async (ClaimsPrincipal principal, Guid? tenantId, IOrganisationMembershipService membershipService, CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(principal, out var userId))
            {
                return Results.Unauthorized();
            }

            var memberships = await membershipService.GetMembershipsForUserAsync(userId, tenantId, cancellationToken).ConfigureAwait(false);
            return Results.Ok(memberships.Select(OrganisationApiMapper.ToMembershipDto));
        })
        .RequireAuthorization();

        endpoints.MapPost("/users/me/organisations/active", async (
            SetActiveOrganisationRequest request,
            IValidator<SetActiveOrganisationRequest> validator,
            ClaimsPrincipal principal,
            IOrganisationMembershipService membershipService,
            IOrganisationService organisationService,
            IOrganisationContextAccessor contextAccessor,
            CancellationToken cancellationToken) =>
        {
            var validationResult = await validator.ValidateAsync(request, cancellationToken).ConfigureAwait(false);
            if (!validationResult.IsValid)
            {
                return Results.ValidationProblem(validationResult.ToDictionary());
            }

            if (!TryGetUserId(principal, out var userId))
            {
                return Results.Unauthorized();
            }

            var organisationId = request.OrganisationId;
            Organisation? organisation = null;

            if (organisationId == Guid.Empty && !string.IsNullOrWhiteSpace(request.OrganisationSlug))
            {
                organisation = await organisationService.GetBySlugAsync(null, request.OrganisationSlug, cancellationToken).ConfigureAwait(false);
                if (organisation is null)
                {
                    return Results.NotFound(new ProblemDetails { Title = "Organisation not found", Detail = "The requested organisation could not be located.", Status = StatusCodes.Status404NotFound });
                }

                organisationId = organisation.Id;
            }

            if (organisationId == Guid.Empty)
            {
                return Results.BadRequest(new ProblemDetails { Title = "Invalid organisation", Detail = "Organisation identifier or slug is required.", Status = StatusCodes.Status400BadRequest });
            }

            var membership = await membershipService.GetMembershipAsync(organisationId, userId, cancellationToken).ConfigureAwait(false);
            if (membership is null)
            {
                return Results.Forbid();
            }

            organisation ??= membership.Organisation ?? await organisationService.GetByIdAsync(organisationId, cancellationToken).ConfigureAwait(false);
            if (organisation is null)
            {
                return Results.NotFound(new ProblemDetails { Title = "Organisation not found", Detail = "The requested organisation could not be located.", Status = StatusCodes.Status404NotFound });
            }

            var context = new OrganisationContext(
                organisation.Id,
                organisation.TenantId,
                organisation.Slug,
                organisation.DisplayName,
                organisation.Metadata);

            using var scope = contextAccessor.BeginScope(context);

            var response = new ActiveOrganisationResponse
            {
                Organisation = OrganisationApiMapper.ToOrganisationDto(organisation),
                RoleIds = membership.RoleAssignments.Select(assignment => assignment.RoleId).ToArray(),
                RequiresTokenRefresh = true
            };

            return Results.Ok(response);
        })
        .RequireAuthorization();

        return endpoints;
    }

    private static bool TryGetUserId(ClaimsPrincipal principal, out Guid userId)
    {
        var value = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out userId);
    }
}
