using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using FluentValidation;
using Identity.Base.Extensions;
using Identity.Base.Identity;
using Identity.Base.Organisations.Abstractions;
using Identity.Base.Organisations.Api.Models;
using Identity.Base.Organisations.Authorization;
using Identity.Base.Organisations.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Identity.Base.Organisations.Api.Modules;

public static class OrganisationInvitationEndpoints
{
    public static IEndpointRouteBuilder MapOrganisationInvitationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapGet("/organisations/{organisationId:guid}/invitations", async (
            Guid organisationId,
            ClaimsPrincipal principal,
            IOrganisationScopeResolver scopeResolver,
            OrganisationInvitationService invitationService,
            CancellationToken cancellationToken) =>
        {
            var scopeResult = await EnsureActorInScopeAsync(principal, scopeResolver, organisationId, cancellationToken).ConfigureAwait(false);
            if (scopeResult is not null)
            {
                return scopeResult;
            }

            var invitations = await invitationService.ListAsync(organisationId, cancellationToken).ConfigureAwait(false);
            var response = invitations.Select(OrganisationApiMapper.ToInvitationDto).ToArray();
            return Results.Ok(response);
        })
        .RequireAuthorization(policy => policy.RequireOrganisationPermission("organisation.members.manage"));

        endpoints.MapPost("/organisations/{organisationId:guid}/invitations", async (
            Guid organisationId,
            CreateOrganisationInvitationRequest request,
            IValidator<CreateOrganisationInvitationRequest> validator,
            ClaimsPrincipal principal,
            IOrganisationScopeResolver scopeResolver,
            OrganisationInvitationService invitationService,
            IOrganisationMembershipService membershipService,
            UserManager<ApplicationUser> userManager,
            CancellationToken cancellationToken) =>
        {
            var validationResult = await validator.ValidateAsync(request, cancellationToken).ConfigureAwait(false);
            if (!validationResult.IsValid)
            {
                return Results.ValidationProblem(validationResult.ToDictionary());
            }

            var scopeResult = await EnsureActorInScopeAsync(principal, scopeResolver, organisationId, cancellationToken).ConfigureAwait(false);
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
                var membership = await membershipService.GetMembershipAsync(organisationId, existingUser.Id, cancellationToken).ConfigureAwait(false);
                if (membership is not null)
                {
                    return Results.Conflict(new ProblemDetails
                    {
                        Title = "User already a member",
                        Detail = "The specified user is already part of this organisation.",
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
                    organisationId,
                    emailToUse,
                    request.RoleIds ?? Array.Empty<Guid>(),
                    actorId,
                    request.ExpiresInHours,
                    cancellationToken).ConfigureAwait(false);

                var dto = OrganisationApiMapper.ToInvitationDto(invitation);
                return Results.Created($"/organisations/{organisationId}/invitations/{dto.Code}", dto);
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound(new ProblemDetails { Title = "Organisation not found", Status = StatusCodes.Status404NotFound });
            }
            catch (OrganisationInvitationAlreadyExistsException ex)
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
        .RequireAuthorization(policy => policy.RequireOrganisationPermission("organisation.members.manage"));

        endpoints.MapDelete("/organisations/{organisationId:guid}/invitations/{code:guid}", async (
            Guid organisationId,
            Guid code,
            ClaimsPrincipal principal,
            IOrganisationScopeResolver scopeResolver,
            OrganisationInvitationService invitationService,
            CancellationToken cancellationToken) =>
        {
            var scopeResult = await EnsureActorInScopeAsync(principal, scopeResolver, organisationId, cancellationToken).ConfigureAwait(false);
            if (scopeResult is not null)
            {
                return scopeResult;
            }

            var revoked = await invitationService.RevokeAsync(organisationId, code, cancellationToken).ConfigureAwait(false);
            return revoked ? Results.NoContent() : Results.NotFound();
        })
        .RequireAuthorization(policy => policy.RequireOrganisationPermission("organisation.members.manage"));

        endpoints.MapGet("/invitations/{code:guid}", async (
            Guid code,
            OrganisationInvitationService invitationService,
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

            return Results.Ok(OrganisationApiMapper.ToInvitationDto(invitation));
        })
        .AllowAnonymous();

        endpoints.MapPost("/invitations/claim", async (
            ClaimOrganisationInvitationRequest request,
            ClaimsPrincipal principal,
            UserManager<ApplicationUser> userManager,
            OrganisationInvitationService invitationService,
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
                    result.OrganisationId,
                    result.OrganisationSlug,
                    result.OrganisationName,
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
        IOrganisationScopeResolver scopeResolver,
        Guid organisationId,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId(principal);
        if (!userId.HasValue)
        {
            return Results.Unauthorized();
        }

        if (organisationId == Guid.Empty)
        {
            return Results.BadRequest(new ProblemDetails { Title = "Invalid organisation", Detail = "Organisation identifier is required.", Status = StatusCodes.Status400BadRequest });
        }

        var inScope = await scopeResolver.IsInScopeAsync(userId.Value, organisationId, cancellationToken).ConfigureAwait(false);
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
