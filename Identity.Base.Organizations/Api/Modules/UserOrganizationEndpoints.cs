using System;
using System.Linq;
using System.Security.Claims;
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

public static class UserOrganizationEndpoints
{
    public static IEndpointRouteBuilder MapUserOrganizationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapGet("/users/me/organizations", async (ClaimsPrincipal principal, Guid? tenantId, IOrganizationMembershipService membershipService, CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(principal, out var userId))
            {
                return Results.Unauthorized();
            }

            var memberships = await membershipService.GetMembershipsForUserAsync(userId, tenantId, cancellationToken).ConfigureAwait(false);
            return Results.Ok(memberships.Select(OrganizationApiMapper.ToMembershipDto));
        })
        .RequireAuthorization();

        endpoints.MapPost("/users/me/organizations/active", async (
            SetActiveOrganizationRequest request,
            IValidator<SetActiveOrganizationRequest> validator,
            ClaimsPrincipal principal,
            IOrganizationMembershipService membershipService,
            IOrganizationService organizationService,
            IOrganizationContextAccessor contextAccessor,
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

            var organizationId = request.OrganizationId;
            Organization? organization = null;

            if (organizationId == Guid.Empty && !string.IsNullOrWhiteSpace(request.OrganizationSlug))
            {
                organization = await organizationService.GetBySlugAsync(null, request.OrganizationSlug, cancellationToken).ConfigureAwait(false);
                if (organization is null)
                {
                    return Results.NotFound(new ProblemDetails { Title = "Organization not found", Detail = "The requested organization could not be located.", Status = StatusCodes.Status404NotFound });
                }

                organizationId = organization.Id;
            }

            if (organizationId == Guid.Empty)
            {
                return Results.BadRequest(new ProblemDetails { Title = "Invalid organization", Detail = "Organization identifier or slug is required.", Status = StatusCodes.Status400BadRequest });
            }

            var membership = await membershipService.GetMembershipAsync(organizationId, userId, cancellationToken).ConfigureAwait(false);
            if (membership is null)
            {
                return Results.Forbid();
            }

            organization ??= membership.Organization ?? await organizationService.GetByIdAsync(organizationId, cancellationToken).ConfigureAwait(false);
            if (organization is null)
            {
                return Results.NotFound(new ProblemDetails { Title = "Organization not found", Detail = "The requested organization could not be located.", Status = StatusCodes.Status404NotFound });
            }

            var context = new OrganizationContext(
                organization.Id,
                organization.TenantId,
                organization.Slug,
                organization.DisplayName,
                organization.Metadata);

            using var scope = contextAccessor.BeginScope(context);

            var response = new ActiveOrganizationResponse
            {
                Organization = OrganizationApiMapper.ToOrganizationDto(organization),
                RoleIds = membership.RoleAssignments.Select(assignment => assignment.RoleId).ToArray(),
                RequiresTokenRefresh = false
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
