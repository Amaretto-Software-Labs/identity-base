using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Identity.Base.Organisations.Abstractions;
using Identity.Base.Organisations.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using OrgSampleApi.Sample.Members;

namespace OrgSampleApi.Hosting.Endpoints;

internal static class SampleMemberEndpoints
{
    public static RouteGroupBuilder MapSampleMemberEndpoints(this RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        group.MapGet("/organisations/{organisationId:guid}/members", HandleMemberListAsync)
            .RequireAuthorization(policy => policy.RequireOrganisationPermission("organisation.members.read"));

        group.MapPatch("/organisations/{organisationId:guid}/members/{userId:guid}", HandleMemberPatchAsync)
            .RequireAuthorization(policy => policy.RequireOrganisationPermission("organisation.members.manage"));

        group.MapDelete("/organisations/{organisationId:guid}/members/{userId:guid}", HandleMemberDeleteAsync)
            .RequireAuthorization(policy => policy.RequireOrganisationPermission("organisation.members.manage"));

        return group;
    }

    private static async Task<IResult> HandleMemberListAsync(
        Guid organisationId,
        ClaimsPrincipal principal,
        IOrganisationScopeResolver scopeResolver,
        OrganisationMemberDirectory memberDirectory,
        CancellationToken cancellationToken)
    {
        var scopeResult = await SampleEndpointHelpers.EnsureActorInScopeAsync(principal, scopeResolver, organisationId, cancellationToken).ConfigureAwait(false);
        if (scopeResult is not null)
        {
            return scopeResult;
        }

        var members = await memberDirectory.GetMembersAsync(organisationId, cancellationToken).ConfigureAwait(false);
        return Results.Ok(members.Select(member => new
        {
            member.OrganisationId,
            member.UserId,
            member.IsPrimary,
            member.RoleIds,
            member.CreatedAtUtc,
            member.UpdatedAtUtc,
            member.Email,
            member.DisplayName
        }));
    }

    private static async Task<IResult> HandleMemberPatchAsync(
        Guid organisationId,
        Guid userId,
        UpdateOrganisationMemberRequest request,
        ClaimsPrincipal principal,
        IOrganisationScopeResolver scopeResolver,
        IOrganisationMembershipService membershipService,
        OrganisationMemberDirectory memberDirectory,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return Results.BadRequest(new ProblemDetails { Title = "Invalid update", Detail = "Request body is required.", Status = StatusCodes.Status400BadRequest });
        }

        var scopeResult = await SampleEndpointHelpers.EnsureActorInScopeAsync(principal, scopeResolver, organisationId, cancellationToken).ConfigureAwait(false);
        if (scopeResult is not null)
        {
            return scopeResult;
        }

        if (!SampleEndpointHelpers.TryGetUserId(principal, out var actorUserId))
        {
            return Results.Unauthorized();
        }

        if (actorUserId == userId)
        {
            return Results.Problem(new ProblemDetails
            {
                Title = "Cannot modify self",
                Detail = "You cannot change your own organisation membership via this endpoint.",
                Status = StatusCodes.Status409Conflict
            });
        }

        if (request.RoleIds is null && !request.IsPrimary.HasValue)
        {
            return Results.BadRequest(new ProblemDetails
            {
                Title = "Invalid update",
                Detail = "Specify roles or primary status to update.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        try
        {
            await membershipService.UpdateMembershipAsync(new OrganisationMembershipUpdateRequest
            {
                OrganisationId = organisationId,
                UserId = userId,
                RoleIds = request.RoleIds?.ToArray(),
                IsPrimary = request.IsPrimary
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new ProblemDetails { Title = "Invalid membership update", Detail = ex.Message, Status = StatusCodes.Status400BadRequest });
        }
        catch (KeyNotFoundException)
        {
            return Results.NotFound(new ProblemDetails { Title = "Membership not found", Detail = "The organisation membership could not be found.", Status = StatusCodes.Status404NotFound });
        }
        catch (InvalidOperationException ex)
        {
            return Results.Conflict(new ProblemDetails { Title = "Membership conflict", Detail = ex.Message, Status = StatusCodes.Status409Conflict });
        }

        var updated = await memberDirectory.GetMemberAsync(organisationId, userId, cancellationToken).ConfigureAwait(false);
        if (updated is null)
        {
            return Results.NotFound(new ProblemDetails { Title = "Membership not found", Detail = "The organisation membership could not be found.", Status = StatusCodes.Status404NotFound });
        }

        return Results.Ok(new
        {
            updated.OrganisationId,
            updated.UserId,
            updated.IsPrimary,
            updated.RoleIds,
            updated.CreatedAtUtc,
            updated.UpdatedAtUtc,
            updated.Email,
            updated.DisplayName
        });
    }

    private static async Task<IResult> HandleMemberDeleteAsync(
        Guid organisationId,
        Guid userId,
        ClaimsPrincipal principal,
        IOrganisationScopeResolver scopeResolver,
        IOrganisationMembershipService membershipService,
        CancellationToken cancellationToken)
    {
        var scopeResult = await SampleEndpointHelpers.EnsureActorInScopeAsync(principal, scopeResolver, organisationId, cancellationToken).ConfigureAwait(false);
        if (scopeResult is not null)
        {
            return scopeResult;
        }

        if (!SampleEndpointHelpers.TryGetUserId(principal, out var actorUserId))
        {
            return Results.Unauthorized();
        }

        if (actorUserId == userId)
        {
            return Results.Problem(new ProblemDetails
            {
                Title = "Cannot remove self",
                Detail = "You cannot remove your own membership from the organisation.",
                Status = StatusCodes.Status409Conflict
            });
        }

        var membership = await membershipService.GetMembershipAsync(organisationId, userId, cancellationToken).ConfigureAwait(false);
        if (membership is null)
        {
            return Results.NotFound(new ProblemDetails { Title = "Membership not found", Detail = "The organisation membership could not be found.", Status = StatusCodes.Status404NotFound });
        }

        await membershipService.RemoveMemberAsync(organisationId, userId, cancellationToken).ConfigureAwait(false);
        return Results.NoContent();
    }
}
