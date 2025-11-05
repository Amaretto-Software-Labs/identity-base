using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Identity.Base.Organizations.Abstractions;
using Identity.Base.Organizations.Authorization;
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

        group.MapGet("/organizations/{organizationId:guid}/members", HandleMemberListAsync)
            .RequireAuthorization(policy => policy.RequireOrganizationPermission(UserOrganizationPermissions.OrganizationMembersRead));

        group.MapPatch("/organizations/{organizationId:guid}/members/{userId:guid}", HandleMemberPatchAsync)
            .RequireAuthorization(policy => policy.RequireOrganizationPermission(UserOrganizationPermissions.OrganizationMembersManage));

        group.MapDelete("/organizations/{organizationId:guid}/members/{userId:guid}", HandleMemberDeleteAsync)
            .RequireAuthorization(policy => policy.RequireOrganizationPermission(UserOrganizationPermissions.OrganizationMembersManage));

        return group;
    }

    private static async Task<IResult> HandleMemberListAsync(
        Guid organizationId,
        ClaimsPrincipal principal,
        IOrganizationScopeResolver scopeResolver,
        OrganizationMemberDirectory memberDirectory,
        CancellationToken cancellationToken)
    {
        var scopeResult = await SampleEndpointHelpers.EnsureActorInScopeAsync(principal, scopeResolver, organizationId, cancellationToken).ConfigureAwait(false);
        if (scopeResult is not null)
        {
            return scopeResult;
        }

        var members = await memberDirectory.GetMembersAsync(organizationId, cancellationToken).ConfigureAwait(false);
        return Results.Ok(members.Select(member => new
        {
            member.OrganizationId,
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
        Guid organizationId,
        Guid userId,
        UpdateOrganizationMemberRequest request,
        ClaimsPrincipal principal,
        IOrganizationScopeResolver scopeResolver,
        IOrganizationMembershipService membershipService,
        OrganizationMemberDirectory memberDirectory,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return Results.BadRequest(new ProblemDetails { Title = "Invalid update", Detail = "Request body is required.", Status = StatusCodes.Status400BadRequest });
        }

        var scopeResult = await SampleEndpointHelpers.EnsureActorInScopeAsync(principal, scopeResolver, organizationId, cancellationToken).ConfigureAwait(false);
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
                Detail = "You cannot change your own organization membership via this endpoint.",
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
            await membershipService.UpdateMembershipAsync(new OrganizationMembershipUpdateRequest
            {
                OrganizationId = organizationId,
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
            return Results.NotFound(new ProblemDetails { Title = "Membership not found", Detail = "The organization membership could not be found.", Status = StatusCodes.Status404NotFound });
        }
        catch (InvalidOperationException ex)
        {
            return Results.Conflict(new ProblemDetails { Title = "Membership conflict", Detail = ex.Message, Status = StatusCodes.Status409Conflict });
        }

        var updated = await memberDirectory.GetMemberAsync(organizationId, userId, cancellationToken).ConfigureAwait(false);
        if (updated is null)
        {
            return Results.NotFound(new ProblemDetails { Title = "Membership not found", Detail = "The organization membership could not be found.", Status = StatusCodes.Status404NotFound });
        }

        return Results.Ok(new
        {
            updated.OrganizationId,
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
        Guid organizationId,
        Guid userId,
        ClaimsPrincipal principal,
        IOrganizationScopeResolver scopeResolver,
        IOrganizationMembershipService membershipService,
        CancellationToken cancellationToken)
    {
        var scopeResult = await SampleEndpointHelpers.EnsureActorInScopeAsync(principal, scopeResolver, organizationId, cancellationToken).ConfigureAwait(false);
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
                Detail = "You cannot remove your own membership from the organization.",
                Status = StatusCodes.Status409Conflict
            });
        }

        var membership = await membershipService.GetMembershipAsync(organizationId, userId, cancellationToken).ConfigureAwait(false);
        if (membership is null)
        {
            return Results.NotFound(new ProblemDetails { Title = "Membership not found", Detail = "The organization membership could not be found.", Status = StatusCodes.Status404NotFound });
        }

        await membershipService.RemoveMemberAsync(organizationId, userId, cancellationToken).ConfigureAwait(false);
        return Results.NoContent();
    }
}
