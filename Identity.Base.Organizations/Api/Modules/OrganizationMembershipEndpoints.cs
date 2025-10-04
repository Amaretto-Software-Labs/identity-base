using System;
using System.Linq;
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

public static class OrganizationMembershipEndpoints
{
    public static IEndpointRouteBuilder MapOrganizationMembershipEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapGet("/organizations/{organizationId:guid}/members", async (Guid organizationId, IOrganizationMembershipService membershipService, CancellationToken cancellationToken) =>
        {
            var members = await membershipService.GetMembersAsync(organizationId, cancellationToken).ConfigureAwait(false);
            return Results.Ok(members.Select(ToDto));
        });

        endpoints.MapPost("/organizations/{organizationId:guid}/members", async (Guid organizationId, AddMembershipRequest request, IValidator<AddMembershipRequest> validator, IOrganizationMembershipService membershipService, CancellationToken cancellationToken) =>
        {
            var validationResult = await validator.ValidateAsync(request, cancellationToken).ConfigureAwait(false);
            if (!validationResult.IsValid)
            {
                return Results.ValidationProblem(validationResult.ToDictionary());
            }

            try
            {
                var membership = await membershipService.AddMemberAsync(new OrganizationMembershipRequest
                {
                    OrganizationId = organizationId,
                    UserId = request.UserId,
                    TenantId = null,
                    IsPrimary = request.IsPrimary,
                    RoleIds = request.RoleIds
                }, cancellationToken).ConfigureAwait(false);

                return Results.Created($"/organizations/{organizationId}/members/{membership.UserId}", ToDto(membership));
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new ProblemDetails { Title = "Invalid membership", Detail = ex.Message, Status = StatusCodes.Status400BadRequest });
            }
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(new ProblemDetails { Title = "Membership conflict", Detail = ex.Message, Status = StatusCodes.Status409Conflict });
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new ProblemDetails { Title = "Organization not found", Detail = ex.Message, Status = StatusCodes.Status404NotFound });
            }
        });

        endpoints.MapPut("/organizations/{organizationId:guid}/members/{userId:guid}", async (Guid organizationId, Guid userId, UpdateMembershipRequest request, IValidator<UpdateMembershipRequest> validator, IOrganizationMembershipService membershipService, CancellationToken cancellationToken) =>
        {
            var validationResult = await validator.ValidateAsync(request, cancellationToken).ConfigureAwait(false);
            if (!validationResult.IsValid)
            {
                return Results.ValidationProblem(validationResult.ToDictionary());
            }

            try
            {
                var membership = await membershipService.UpdateMembershipAsync(new OrganizationMembershipUpdateRequest
                {
                    OrganizationId = organizationId,
                    UserId = userId,
                    IsPrimary = request.IsPrimary,
                    RoleIds = request.RoleIds
                }, cancellationToken).ConfigureAwait(false);

                return Results.Ok(ToDto(membership));
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new ProblemDetails { Title = "Invalid membership update", Detail = ex.Message, Status = StatusCodes.Status400BadRequest });
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new ProblemDetails { Title = "Membership not found", Detail = ex.Message, Status = StatusCodes.Status404NotFound });
            }
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(new ProblemDetails { Title = "Membership conflict", Detail = ex.Message, Status = StatusCodes.Status409Conflict });
            }
        });

        endpoints.MapDelete("/organizations/{organizationId:guid}/members/{userId:guid}", async (Guid organizationId, Guid userId, IOrganizationMembershipService membershipService, CancellationToken cancellationToken) =>
        {
            await membershipService.RemoveMemberAsync(organizationId, userId, cancellationToken).ConfigureAwait(false);
            return Results.NoContent();
        });

        return endpoints;
    }

    private static OrganizationMembershipDto ToDto(OrganizationMembership membership)
        => new()
        {
            OrganizationId = membership.OrganizationId,
            UserId = membership.UserId,
            TenantId = membership.TenantId,
            IsPrimary = membership.IsPrimary,
            RoleIds = membership.RoleAssignments.Select(assignment => assignment.RoleId).ToArray(),
            CreatedAtUtc = membership.CreatedAtUtc,
            UpdatedAtUtc = membership.UpdatedAtUtc
        };
}
