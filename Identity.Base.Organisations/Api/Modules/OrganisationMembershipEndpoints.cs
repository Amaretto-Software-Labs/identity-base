using System;
using System.Linq;
using System.Security.Claims;
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

public static class OrganisationMembershipEndpoints
{
    public static IEndpointRouteBuilder MapOrganisationMembershipEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapGet("/organisations/{organisationId:guid}/members", async (
            Guid organisationId,
            [AsParameters] OrganisationMemberListQuery query,
            ClaimsPrincipal principal,
            IOrganisationScopeResolver scopeResolver,
            IOrganisationMembershipService membershipService,
            CancellationToken cancellationToken) =>
        {
            var scopeResult = await EnsureActorInScopeAsync(principal, scopeResolver, organisationId, null, cancellationToken).ConfigureAwait(false);
            if (scopeResult is not null)
            {
                return scopeResult;
            }

            var request = new OrganisationMemberListRequest
            {
                OrganisationId = organisationId,
                Page = query.Page,
                PageSize = query.PageSize,
                Search = query.Search,
                RoleId = query.RoleId,
                IsPrimary = query.IsPrimary,
                Sort = ResolveSort(query.Sort)
            };

            var members = await membershipService.GetMembersAsync(request, cancellationToken).ConfigureAwait(false);
            return Results.Ok(OrganisationApiMapper.ToMemberListResponse(members));
        })
        .RequireAuthorization(policy => policy.RequireOrganisationPermission("organisation.members.read"));

        endpoints.MapPost("/organisations/{organisationId:guid}/members", async (Guid organisationId, AddMembershipRequest request, IValidator<AddMembershipRequest> validator, ClaimsPrincipal principal, IOrganisationScopeResolver scopeResolver, IOrganisationMembershipService membershipService, CancellationToken cancellationToken) =>
        {
            var validationResult = await validator.ValidateAsync(request, cancellationToken).ConfigureAwait(false);
            if (!validationResult.IsValid)
            {
                return Results.ValidationProblem(validationResult.ToDictionary());
            }

            var scopeResult = await EnsureActorInScopeAsync(principal, scopeResolver, organisationId, request.UserId, cancellationToken).ConfigureAwait(false);
            if (scopeResult is not null)
            {
                return scopeResult;
            }

            try
            {
                var membership = await membershipService.AddMemberAsync(new OrganisationMembershipRequest
                {
                    OrganisationId = organisationId,
                    UserId = request.UserId,
                    TenantId = null,
                    IsPrimary = request.IsPrimary,
                    RoleIds = request.RoleIds
                }, cancellationToken).ConfigureAwait(false);

                return Results.Created($"/organisations/{organisationId}/members/{membership.UserId}", OrganisationApiMapper.ToMembershipDto(membership));
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
                return Results.NotFound(new ProblemDetails { Title = "Organisation not found", Detail = ex.Message, Status = StatusCodes.Status404NotFound });
            }
        })
        .RequireAuthorization(policy => policy.RequireOrganisationPermission("organisation.members.manage"));

        endpoints.MapPut("/organisations/{organisationId:guid}/members/{userId:guid}", async (Guid organisationId, Guid userId, UpdateMembershipRequest request, IValidator<UpdateMembershipRequest> validator, ClaimsPrincipal principal, IOrganisationScopeResolver scopeResolver, IOrganisationMembershipService membershipService, CancellationToken cancellationToken) =>
        {
            var validationResult = await validator.ValidateAsync(request, cancellationToken).ConfigureAwait(false);
            if (!validationResult.IsValid)
            {
                return Results.ValidationProblem(validationResult.ToDictionary());
            }

            var scopeResult = await EnsureActorInScopeAsync(principal, scopeResolver, organisationId, userId, cancellationToken).ConfigureAwait(false);
            if (scopeResult is not null)
            {
                return scopeResult;
            }

            try
            {
                var membership = await membershipService.UpdateMembershipAsync(new OrganisationMembershipUpdateRequest
                {
                    OrganisationId = organisationId,
                    UserId = userId,
                    IsPrimary = request.IsPrimary,
                    RoleIds = request.RoleIds
                }, cancellationToken).ConfigureAwait(false);

                return Results.Ok(OrganisationApiMapper.ToMembershipDto(membership));
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
        })
        .RequireAuthorization(policy => policy.RequireOrganisationPermission("organisation.members.manage"));

        endpoints.MapDelete("/organisations/{organisationId:guid}/members/{userId:guid}", async (Guid organisationId, Guid userId, ClaimsPrincipal principal, IOrganisationScopeResolver scopeResolver, IOrganisationMembershipService membershipService, CancellationToken cancellationToken) =>
        {
            var scopeResult = await EnsureActorInScopeAsync(principal, scopeResolver, organisationId, userId, cancellationToken).ConfigureAwait(false);
            if (scopeResult is not null)
            {
                return scopeResult;
            }

            await membershipService.RemoveMemberAsync(organisationId, userId, cancellationToken).ConfigureAwait(false);
            return Results.NoContent();
        })
        .RequireAuthorization(policy => policy.RequireOrganisationPermission("organisation.members.manage"));

        return endpoints;
    }

    private static async Task<IResult?> EnsureActorInScopeAsync(
        ClaimsPrincipal principal,
        IOrganisationScopeResolver scopeResolver,
        Guid organisationId,
        Guid? targetUserId,
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
        if (!inScope && (!targetUserId.HasValue || targetUserId.Value != actorUserId))
        {
            return Results.Forbid();
        }

        return null;
    }

    private static OrganisationMemberSort ResolveSort(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return OrganisationMemberSort.CreatedAtDescending;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "createdat" => OrganisationMemberSort.CreatedAtAscending,
            "createdat:asc" => OrganisationMemberSort.CreatedAtAscending,
            "createdat:desc" => OrganisationMemberSort.CreatedAtDescending,
            "-createdat" => OrganisationMemberSort.CreatedAtDescending,
            _ => OrganisationMemberSort.CreatedAtDescending
        };
    }

    private static bool TryGetUserId(ClaimsPrincipal principal, out Guid userId)
    {
        var value = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out userId);
    }
}
