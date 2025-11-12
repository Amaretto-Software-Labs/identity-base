using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using Identity.Base.Abstractions;
using Identity.Base.Extensions;
using Identity.Base.Features.Authentication.EmailManagement;
using Identity.Base.Identity;
using Identity.Base.Logging;
using Identity.Base.Options;
using Identity.Base.Organizations.Services;
using Identity.Base.Lifecycle;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Mvc;
using OrgSampleApi.Sample;
using OrgSampleApi.Sample.Invitations;

namespace OrgSampleApi.Hosting.Endpoints;

internal static class SampleRegistrationEndpoints
{
    public static RouteGroupBuilder MapSampleRegistrationEndpoints(this RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        group.MapPost("/invitations/register", HandleInvitationRegistrationAsync)
            .AllowAnonymous()
            .WithName("RegisterWithInvitation")
            .WithSummary("Registers a new user through an invitation and assigns organization membership.")
            .Produces(StatusCodes.Status202Accepted)
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status404NotFound);

        return group;
    }

    private static async Task<IResult> HandleInvitationRegistrationAsync(
        HttpContext httpContext,
        InvitationRegistrationRequest request,
        IValidator<InvitationRegistrationRequest> validator,
        OrganizationInvitationService invitationService,
        UserManager<ApplicationUser> userManager,
        IAccountEmailService accountEmailService,
        IOptions<RegistrationOptions> registrationOptions,
        IUserLifecycleHookDispatcher lifecycleDispatcher,
        ILoggerFactory loggerFactory,
        ILogSanitizer logSanitizer,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["request"] = new[] { "Request payload is required." } });
        }

        request.Metadata ??= new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        var validationResult = await validator.ValidateAsync(request, cancellationToken).ConfigureAwait(false);
        if (!validationResult.IsValid)
        {
            return Results.ValidationProblem(validationResult.ToDictionary());
        }

        var invitation = await invitationService.FindAsync(request.InvitationCode, cancellationToken).ConfigureAwait(false);
        if (invitation is null)
        {
            return Results.NotFound(new ProblemDetails { Title = "Invitation not found", Detail = "The invitation is missing, expired, or already used.", Status = StatusCodes.Status404NotFound });
        }

        if (!string.Equals(invitation.Email, request.Email, StringComparison.OrdinalIgnoreCase))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["email"] = new[] { "Invitation email does not match the supplied address." } });
        }

        httpContext.Items[OrgSampleHttpContextKeys.InvitationCode] = invitation.Code;

        var registration = registrationOptions.Value;

        var user = new ApplicationUser
        {
            Email = request.Email,
            UserName = request.Email,
            DisplayName = SampleEndpointHelpers.ResolveDisplayName(request.Metadata, registration)
        };

        user.SetProfileMetadata(request.Metadata);

        var lifecycleContext = new UserLifecycleContext(
            UserLifecycleEvent.Registration,
            user,
            Source: nameof(HandleInvitationRegistrationAsync),
            Items: new Dictionary<string, object?>
            {
                ["InvitationCode"] = invitation.Code,
                ["Invitation"] = invitation
            });

        try
        {
            await lifecycleDispatcher.EnsureCanRegisterAsync(lifecycleContext, cancellationToken);
        }
        catch (LifecycleHookRejectedException exception)
        {
            return Results.Problem(exception.Message, statusCode: StatusCodes.Status400BadRequest);
        }

        var createResult = await userManager.CreateAsync(user, request.Password).ConfigureAwait(false);
        if (!createResult.Succeeded)
        {
            return Results.ValidationProblem(createResult.ToDictionary());
        }

        var logger = loggerFactory.CreateLogger("OrgSample.InvitationRegistration");

        try
        {
            await accountEmailService.SendConfirmationEmailAsync(user, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogError(exception, "Failed to send confirmation email for {Email}", logSanitizer.RedactEmail(user.Email));
            return Results.Problem("Failed to dispatch confirmation email.", statusCode: StatusCodes.Status500InternalServerError);
        }

        await lifecycleDispatcher.NotifyUserRegisteredAsync(lifecycleContext, cancellationToken);

        try
        {
            var acceptance = await invitationService.AcceptAsync(invitation.Code, user, cancellationToken).ConfigureAwait(false);
            if (acceptance is null)
            {
                return Results.Problem("Invitation could not be processed.", statusCode: StatusCodes.Status409Conflict);
            }
        }
        catch (InvalidOperationException exception)
        {
            logger.LogError(exception, "Failed to apply invitation {InvitationCode} for {Email}", invitation.Code, logSanitizer.RedactEmail(user.Email));
            return Results.Problem(exception.Message, statusCode: StatusCodes.Status409Conflict);
        }

        var correlationId = Guid.NewGuid().ToString("N");
        return Results.Accepted($"/auth/register/{correlationId}", new { correlationId });
    }
}
