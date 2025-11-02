using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Identity.Base.Identity;
using Identity.Base.Logging;
using Identity.Base.Organisations.Abstractions;
using Identity.Base.Organisations.Authorization;
using Identity.Base.Organisations.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrgSampleApi.Sample.Invitations;

namespace OrgSampleApi.Hosting.Endpoints;

internal static class SampleInvitationEndpoints
{
    public static RouteGroupBuilder MapSampleInvitationEndpoints(this RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        group.MapGet("/organisations/{organisationId:guid}/invitations", HandleInvitationListAsync)
            .RequireAuthorization(policy => policy.RequireOrganisationPermission("organisation.members.manage"));

        group.MapPost("/organisations/{organisationId:guid}/invitations", HandleInvitationCreateAsync)
            .RequireAuthorization(policy => policy.RequireOrganisationPermission("organisation.members.manage"));

        group.MapDelete("/organisations/{organisationId:guid}/invitations/{code:guid}", HandleInvitationDeleteAsync)
            .RequireAuthorization(policy => policy.RequireOrganisationPermission("organisation.members.manage"));

        group.MapPost("/invitations/claim", HandleInvitationClaimAsync)
            .RequireAuthorization();

        group.MapGet("/invitations/{code:guid}", HandleInvitationDetailsAsync)
            .AllowAnonymous();

        return group;
    }

    private static async Task<IResult> HandleInvitationListAsync(
        Guid organisationId,
        ClaimsPrincipal principal,
        IOrganisationScopeResolver scopeResolver,
        OrganisationInvitationService invitationService,
        UserManager<ApplicationUser> userManager,
        IOptions<InvitationLinkOptions> linkOptions,
        CancellationToken cancellationToken)
    {
        var scopeResult = await SampleEndpointHelpers.EnsureActorInScopeAsync(principal, scopeResolver, organisationId, cancellationToken).ConfigureAwait(false);
        if (scopeResult is not null)
        {
            return scopeResult;
        }

        var invitations = await invitationService.ListAsync(organisationId, cancellationToken).ConfigureAwait(false);
        var options = linkOptions.Value;
        var response = new List<InvitationResponse>(invitations.Count);
        foreach (var invitation in invitations)
        {
            var existingUser = await userManager.FindByEmailAsync(invitation.Email).ConfigureAwait(false);
            var (registerUrl, claimUrl) = SampleEndpointHelpers.ResolveInvitationLinks(options, invitation.Code, existingUser is not null);

            response.Add(new InvitationResponse
            {
                Code = invitation.Code,
                Email = invitation.Email,
                RoleIds = invitation.RoleIds,
                ExpiresAtUtc = invitation.ExpiresAtUtc,
                OrganisationName = invitation.OrganisationName,
                OrganisationSlug = invitation.OrganisationSlug,
                IsExistingUser = existingUser is not null,
                RegisterUrl = registerUrl,
                ClaimUrl = claimUrl
            });
        }

        return Results.Ok(response);
    }

    private static async Task<IResult> HandleInvitationCreateAsync(
        Guid organisationId,
        CreateInvitationRequest request,
        ClaimsPrincipal principal,
        IOrganisationScopeResolver scopeResolver,
        OrganisationInvitationService invitationService,
        UserManager<ApplicationUser> userManager,
        IOptions<InvitationLinkOptions> linkOptions,
        IOrganisationMembershipService membershipService,
        ILogger<OrganisationInvitationService> invitationLogger,
        CancellationToken cancellationToken)
    {
        var scopeResult = await SampleEndpointHelpers.EnsureActorInScopeAsync(principal, scopeResolver, organisationId, cancellationToken).ConfigureAwait(false);
        if (scopeResult is not null)
        {
            return scopeResult;
        }

        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["email"] = ["Email is required."] });
        }

        var emailAttribute = new EmailAddressAttribute();
        if (!emailAttribute.IsValid(request.Email))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["email"] = ["Email format is invalid."] });
        }

        var actorUserId = SampleEndpointHelpers.TryGetUserId(principal, out var userId) ? userId : (Guid?)null;

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var existingUser = await userManager.FindByEmailAsync(normalizedEmail).ConfigureAwait(false);
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

        try
        {
            var invitation = await invitationService.CreateAsync(
                organisationId,
                request.Email,
                request.RoleIds ?? Array.Empty<Guid>(),
                actorUserId,
                request.ExpiresInHours,
                cancellationToken).ConfigureAwait(false);

            var options = linkOptions.Value;
            existingUser ??= await userManager.FindByEmailAsync(invitation.Email).ConfigureAwait(false);
            var (registerUrl, claimUrl) = SampleEndpointHelpers.ResolveInvitationLinks(options, invitation.Code, existingUser is not null);

            if (existingUser is not null)
            {
                invitationLogger.LogInformation(
                    "Invitation {InvitationCode} for {Email} (existing user) created. Claim URL: {ClaimUrl}",
                    invitation.Code,
                    invitation.Email,
                    claimUrl);
            }
            else
            {
                invitationLogger.LogInformation(
                    "Invitation {InvitationCode} for {Email} (new user) created. Registration URL: {RegisterUrl}",
                    invitation.Code,
                    invitation.Email,
                    registerUrl);
            }

            var response = new InvitationResponse
            {
                Code = invitation.Code,
                Email = invitation.Email,
                RoleIds = invitation.RoleIds,
                ExpiresAtUtc = invitation.ExpiresAtUtc,
                OrganisationName = invitation.OrganisationName,
                OrganisationSlug = invitation.OrganisationSlug,
                IsExistingUser = existingUser is not null,
                RegisterUrl = registerUrl,
                ClaimUrl = claimUrl
            };

            return Results.Created($"/sample/organisations/{organisationId}/invitations/{invitation.Code}", response);
        }
        catch (KeyNotFoundException)
        {
            return Results.NotFound();
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
    }

    private static async Task<IResult> HandleInvitationDeleteAsync(
        Guid organisationId,
        Guid code,
        ClaimsPrincipal principal,
        IOrganisationScopeResolver scopeResolver,
        OrganisationInvitationService invitationService,
        CancellationToken cancellationToken)
    {
        var scopeResult = await SampleEndpointHelpers.EnsureActorInScopeAsync(principal, scopeResolver, organisationId, cancellationToken).ConfigureAwait(false);
        if (scopeResult is not null)
        {
            return scopeResult;
        }

        var revoked = await invitationService.RevokeAsync(organisationId, code, cancellationToken).ConfigureAwait(false);
        return revoked ? Results.NoContent() : Results.NotFound();
    }

    private static async Task<IResult> HandleInvitationClaimAsync(
        ClaimInvitationRequest request,
        ClaimsPrincipal principal,
        UserManager<ApplicationUser> userManager,
        OrganisationInvitationService invitationService,
        CancellationToken cancellationToken)
    {
        if (request.Code == Guid.Empty)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["code"] = ["Invitation code is required."] });
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
    }

    private static async Task<IResult> HandleInvitationDetailsAsync(
        Guid code,
        OrganisationInvitationService invitationService,
        UserManager<ApplicationUser> userManager,
        IOptions<InvitationLinkOptions> linkOptions,
        CancellationToken cancellationToken)
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

        var existingUser = await userManager.FindByEmailAsync(invitation.Email).ConfigureAwait(false);
        var options = linkOptions.Value;
        var (registerUrl, claimUrl) = SampleEndpointHelpers.ResolveInvitationLinks(options, invitation.Code, existingUser is not null);

        return Results.Ok(new
        {
            invitation.Code,
            invitation.Email,
            invitation.OrganisationId,
            invitation.OrganisationName,
            invitation.OrganisationSlug,
            invitation.RoleIds,
            invitation.ExpiresAtUtc,
            IsExistingUser = existingUser is not null,
            RegisterUrl = registerUrl,
            ClaimUrl = claimUrl
        });
    }
}
