using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using FluentValidation;
using Identity.Base.Extensions;
using Identity.Base.Identity;
using Identity.Base.Organizations.Abstractions;
using Identity.Base.Organizations.Api.Models;
using Identity.Base.Organizations.Authorization;
using Identity.Base.Organizations.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Identity.Base.Organizations.Api.Modules;

public static class OrganizationInvitationEndpoints
{
    public static IEndpointRouteBuilder MapOrganizationInvitationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapGet("/organizations/{organizationId:guid}/invitations", async (
            Guid organizationId,
            ClaimsPrincipal principal,
            IOrganizationScopeResolver scopeResolver,
            OrganizationInvitationService invitationService,
            CancellationToken cancellationToken) =>
        {
            var scopeResult = await EnsureActorInScopeAsync(principal, scopeResolver, organizationId, cancellationToken).ConfigureAwait(false);
            if (scopeResult is not null)
            {
                return scopeResult;
            }

            var invitations = await invitationService.ListAsync(organizationId, cancellationToken).ConfigureAwait(false);
            var response = invitations.Select(OrganizationApiMapper.ToInvitationDto).ToArray();
            return Results.Ok(response);
        })
        .RequireAuthorization(policy => policy.RequireOrganizationPermission("organization.members.manage"));

        endpoints.MapPost("/organizations/{organizationId:guid}/invitations", async (
            Guid organizationId,
            CreateOrganizationInvitationRequest request,
            IValidator<CreateOrganizationInvitationRequest> validator,
            ClaimsPrincipal principal,
            IOrganizationScopeResolver scopeResolver,
            OrganizationInvitationService invitationService,
            IOrganizationMembershipService membershipService,
            UserManager<ApplicationUser> userManager,
            CancellationToken cancellationToken) =>
        {
            var validationResult = await validator.ValidateAsync(request, cancellationToken).ConfigureAwait(false);
            if (!validationResult.IsValid)
            {
                return Results.ValidationProblem(validationResult.ToDictionary());
            }

            var scopeResult = await EnsureActorInScopeAsync(principal, scopeResolver, organizationId, cancellationToken).ConfigureAwait(false);
            if (scopeResult is not null)
            {
                return scopeResult;
            }

            var emailAttribute = new EmailAddressAttribute();
            if (!emailAttribute.IsValid(request.Email))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["email"] = new[] { "Email format is invalid." } });
            }

            var normalizedEmail = userManager.NormalizeEmail(request.Email);
            var existingUser = string.IsNullOrWhiteSpace(normalizedEmail)
                ? null
                : await userManager.FindByEmailAsync(normalizedEmail).ConfigureAwait(false);

            if (existingUser is not null)
            {
                var membership = await membershipService.GetMembershipAsync(organizationId, existingUser.Id, cancellationToken).ConfigureAwait(false);
                if (membership is not null)
                {
                    return Results.Conflict(new ProblemDetails
                    {
                        Title = "User already a member",
                        Detail = "The specified user is already part of this organization.",
                        Status = StatusCodes.Status409Conflict
                    });
                }
            }

            var emailToUse = normalizedEmail ?? request.Email.Trim();
            if (string.IsNullOrWhiteSpace(emailToUse))
            {
                return Results.BadRequest(new ProblemDetails
                {
                    Title = "Invalid email",
                    Detail = "Email address normalization failed.",
                    Status = StatusCodes.Status400BadRequest
                });
            }

            var actorId = GetUserId(principal);

            try
            {
                var invitation = await invitationService.CreateAsync(
                    organizationId,
                    emailToUse,
                    request.RoleIds ?? Array.Empty<Guid>(),
                    actorId,
                    request.ExpiresInHours,
                    cancellationToken).ConfigureAwait(false);

                var dto = OrganizationApiMapper.ToInvitationDto(invitation);
                return Results.Created($"/organizations/{organizationId}/invitations/{dto.Code}", dto);
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound(new ProblemDetails { Title = "Organization not found", Status = StatusCodes.Status404NotFound });
            }
            catch (OrganizationInvitationAlreadyExistsException ex)
            {
                return Results.Conflict(new ProblemDetails
                {
                    Title = "Invitation already exists",
                    Detail = ex.Message,
                    Status = StatusCodes.Status409Conflict
                });
            }
            catch (InvalidOperationException ex)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["roles"] = new[] { ex.Message } });
            }
        })
        .RequireAuthorization(policy => policy.RequireOrganizationPermission("organization.members.manage"));

        endpoints.MapDelete("/organizations/{organizationId:guid}/invitations/{code:guid}", async (
            Guid organizationId,
            Guid code,
            ClaimsPrincipal principal,
            IOrganizationScopeResolver scopeResolver,
            OrganizationInvitationService invitationService,
            CancellationToken cancellationToken) =>
        {
            var scopeResult = await EnsureActorInScopeAsync(principal, scopeResolver, organizationId, cancellationToken).ConfigureAwait(false);
            if (scopeResult is not null)
            {
                return scopeResult;
            }

            var revoked = await invitationService.RevokeAsync(organizationId, code, cancellationToken).ConfigureAwait(false);
            return revoked ? Results.NoContent() : Results.NotFound();
        })
        .RequireAuthorization(policy => policy.RequireOrganizationPermission("organization.members.manage"));

        endpoints.MapGet("/invitations/{code:guid}", async (
            Guid code,
            OrganizationInvitationService invitationService,
            CancellationToken cancellationToken) =>
        {
            if (code == Guid.Empty)
            {
                return Results.BadRequest(new ProblemDetails { Title = "Invalid invitation code." });
            }

            var invitation = await invitationService.FindAsync(code, cancellationToken).ConfigureAwait(false);
            if (invitation is null)
            {
                return Results.NotFound();
            }

            return Results.Ok(OrganizationApiMapper.ToInvitationDto(invitation));
        })
        .AllowAnonymous();

        endpoints.MapPost("/invitations/claim", async (
            ClaimOrganizationInvitationRequest request,
            ClaimsPrincipal principal,
            UserManager<ApplicationUser> userManager,
            OrganizationInvitationService invitationService,
            CancellationToken cancellationToken) =>
        {
            if (request.Code == Guid.Empty)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["code"] = new[] { "Invitation code is required." } });
            }

            var user = await userManager.GetUserAsync(principal).ConfigureAwait(false);
            if (user is null)
            {
                return Results.Unauthorized();
            }

            try
            {
                var result = await invitationService.AcceptAsync(request.Code, user, cancellationToken).ConfigureAwait(false);
                if (result is null)
                {
                    return Results.NotFound();
                }

                return Results.Ok(new
                {
                    result.OrganizationId,
                    result.OrganizationSlug,
                    result.OrganizationName,
                    result.RoleIds,
                    result.WasExistingMember,
                    result.WasExistingUser,
                    RequiresTokenRefresh = true
                });
            }
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(new { Message = ex.Message });
            }
        })
        .RequireAuthorization();

        return endpoints;
    }

    private static async Task<IResult?> EnsureActorInScopeAsync(
        ClaimsPrincipal principal,
        IOrganizationScopeResolver scopeResolver,
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId(principal);
        if (!userId.HasValue)
        {
            return Results.Unauthorized();
        }

        if (organizationId == Guid.Empty)
        {
            return Results.BadRequest(new ProblemDetails { Title = "Invalid organization", Detail = "Organization identifier is required.", Status = StatusCodes.Status400BadRequest });
        }

        var inScope = await scopeResolver.IsInScopeAsync(userId.Value, organizationId, cancellationToken).ConfigureAwait(false);
        if (!inScope)
        {
            return Results.Forbid();
        }

        return null;
    }

    private static Guid? GetUserId(ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var userId) ? userId : null;
    }
}
