using System;
using System.Linq;
using System.Security.Claims;
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

public static class OrganizationMembershipEndpoints
{
    public static IEndpointRouteBuilder MapOrganizationMembershipEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapGet("/organizations/{organizationId:guid}/members", async (
            Guid organizationId,
            [AsParameters] OrganizationMemberListQuery query,
            ClaimsPrincipal principal,
            IOrganizationScopeResolver scopeResolver,
            IOrganizationMembershipService membershipService,
            CancellationToken cancellationToken) =>
        {
            var scopeResult = await EnsureActorInScopeAsync(principal, scopeResolver, organizationId, null, cancellationToken).ConfigureAwait(false);
            if (scopeResult is not null)
            {
                return scopeResult;
            }

            var request = new OrganizationMemberListRequest
            {
                OrganizationId = organizationId,
                Page = query.Page,
                PageSize = query.PageSize,
                Search = query.Search,
                RoleId = query.RoleId,
                IsPrimary = query.IsPrimary,
                Sort = ResolveSort(query.Sort)
            };

            var members = await membershipService.GetMembersAsync(request, cancellationToken).ConfigureAwait(false);
            return Results.Ok(OrganizationApiMapper.ToMemberListResponse(members));
        })
        .RequireAuthorization(policy => policy.RequireOrganizationPermission(AdminOrganizationPermissions.OrganizationMembersRead));

        endpoints.MapPost("/organizations/{organizationId:guid}/members", async (Guid organizationId, AddMembershipRequest request, IValidator<AddMembershipRequest> validator, ClaimsPrincipal principal, IOrganizationScopeResolver scopeResolver, IOrganizationMembershipService membershipService, CancellationToken cancellationToken) =>
        {
            var validationResult = await validator.ValidateAsync(request, cancellationToken).ConfigureAwait(false);
            if (!validationResult.IsValid)
            {
                return Results.ValidationProblem(validationResult.ToDictionary());
            }

            var scopeResult = await EnsureActorInScopeAsync(principal, scopeResolver, organizationId, request.UserId, cancellationToken).ConfigureAwait(false);
            if (scopeResult is not null)
            {
                return scopeResult;
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

                return Results.Created($"/organizations/{organizationId}/members/{membership.UserId}", OrganizationApiMapper.ToMembershipDto(membership));
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
        })
        .RequireAuthorization(policy => policy.RequireOrganizationPermission(AdminOrganizationPermissions.OrganizationMembersManage));

        endpoints.MapPut("/organizations/{organizationId:guid}/members/{userId:guid}", async (Guid organizationId, Guid userId, UpdateMembershipRequest request, IValidator<UpdateMembershipRequest> validator, ClaimsPrincipal principal, IOrganizationScopeResolver scopeResolver, IOrganizationMembershipService membershipService, CancellationToken cancellationToken) =>
        {
            var validationResult = await validator.ValidateAsync(request, cancellationToken).ConfigureAwait(false);
            if (!validationResult.IsValid)
            {
                return Results.ValidationProblem(validationResult.ToDictionary());
            }

            var scopeResult = await EnsureActorInScopeAsync(principal, scopeResolver, organizationId, userId, cancellationToken).ConfigureAwait(false);
            if (scopeResult is not null)
            {
                return scopeResult;
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

                return Results.Ok(OrganizationApiMapper.ToMembershipDto(membership));
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
        .RequireAuthorization(policy => policy.RequireOrganizationPermission(AdminOrganizationPermissions.OrganizationMembersManage));

        endpoints.MapDelete("/organizations/{organizationId:guid}/members/{userId:guid}", async (Guid organizationId, Guid userId, ClaimsPrincipal principal, IOrganizationScopeResolver scopeResolver, IOrganizationMembershipService membershipService, CancellationToken cancellationToken) =>
        {
            var scopeResult = await EnsureActorInScopeAsync(principal, scopeResolver, organizationId, userId, cancellationToken).ConfigureAwait(false);
            if (scopeResult is not null)
            {
                return scopeResult;
            }

            await membershipService.RemoveMemberAsync(organizationId, userId, cancellationToken).ConfigureAwait(false);
            return Results.NoContent();
        })
        .RequireAuthorization(policy => policy.RequireOrganizationPermission(AdminOrganizationPermissions.OrganizationMembersManage));

        return endpoints;
    }

    private static async Task<IResult?> EnsureActorInScopeAsync(
        ClaimsPrincipal principal,
        IOrganizationScopeResolver scopeResolver,
        Guid organizationId,
        Guid? targetUserId,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(principal, out var actorUserId))
        {
            return Results.Unauthorized();
        }

        if (organizationId == Guid.Empty)
        {
            return Results.BadRequest(new ProblemDetails { Title = "Invalid organization", Detail = "Organization identifier is required.", Status = StatusCodes.Status400BadRequest });
        }

        var inScope = await scopeResolver.IsInScopeAsync(actorUserId, organizationId, cancellationToken).ConfigureAwait(false);
        if (!inScope && (!targetUserId.HasValue || targetUserId.Value != actorUserId))
        {
            return Results.Forbid();
        }

        return null;
    }

    private static OrganizationMemberSort ResolveSort(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return OrganizationMemberSort.CreatedAtDescending;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "createdat" => OrganizationMemberSort.CreatedAtAscending,
            "createdat:asc" => OrganizationMemberSort.CreatedAtAscending,
            "createdat:desc" => OrganizationMemberSort.CreatedAtDescending,
            "-createdat" => OrganizationMemberSort.CreatedAtDescending,
            _ => OrganizationMemberSort.CreatedAtDescending
        };
    }

    private static bool TryGetUserId(ClaimsPrincipal principal, out Guid userId)
    {
        var value = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out userId);
    }
}
