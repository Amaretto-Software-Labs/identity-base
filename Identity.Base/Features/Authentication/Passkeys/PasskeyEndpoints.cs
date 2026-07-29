using System.Security.Claims;
using Identity.Base.Features.Security;
using Identity.Base.Identity;
using Identity.Base.Logging;
using Identity.Base.Lifecycle;
using Identity.Base.Options;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;
using OpenIddict.Validation.AspNetCore;

namespace Identity.Base.Features.Authentication.Passkeys;

public static class PasskeyEndpoints
{
    public static RouteGroupBuilder MapPasskeyAuthenticationEndpoints(
        this RouteGroupBuilder group,
        bool signupEnabled)
    {
        group.MapGet("/passkeys/configuration", GetConfiguration)
            .WithName("GetPasskeyConfiguration")
            .WithTags("Passkeys")
            .RequireRateLimiting(PasskeyRateLimitPolicies.Configuration);

        group.MapPost("/passkeys/authentication/options", MakeAuthenticationOptionsAsync)
            .WithName("MakePasskeyAuthenticationOptions")
            .WithTags("Passkeys")
            .RequireRateLimiting(PasskeyRateLimitPolicies.AuthenticationOptions);

        group.MapPost("/passkeys/authentication", AuthenticateAsync)
            .WithName("AuthenticateWithPasskey")
            .WithTags("Passkeys")
            .WithMetadata(new RequestSizeLimitAttribute(64 * 1024))
            .RequireRateLimiting(PasskeyRateLimitPolicies.Authentication);

        if (signupEnabled)
        {
            group.MapPasskeySignupEndpoints();
        }
        group.MapPasskeyRecoveryEndpoints();
        return group;
    }

    public static IEndpointRouteBuilder MapPasskeyManagementEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var listGroup = endpoints
            .MapGroup("/users/me/passkeys")
            .RequireAuthorization(new AuthorizeAttribute
            {
                AuthenticationSchemes =
                    $"{IdentityConstants.ApplicationScheme},{OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme}"
            });

        listGroup.MapGet("/", ListAsync)
            .WithName("ListPasskeys")
            .WithTags("Passkeys");

        var mutationGroup = endpoints
            .MapGroup("/users/me/passkeys")
            .RequireAuthorization(new AuthorizeAttribute
            {
                AuthenticationSchemes = IdentityConstants.ApplicationScheme
            })
            .AddEndpointFilter<BrowserOriginGuardEndpointFilter>();

        mutationGroup.MapPost("/creation/options", MakeCreationOptionsAsync)
            .WithName("MakePasskeyCreationOptions")
            .WithTags("Passkeys")
            .RequireRateLimiting(PasskeyRateLimitPolicies.Creation);
        mutationGroup.MapPost("/", CreateAsync)
            .WithName("CreatePasskey")
            .WithTags("Passkeys")
            .WithMetadata(new RequestSizeLimitAttribute(64 * 1024))
            .RequireRateLimiting(PasskeyRateLimitPolicies.Creation);
        mutationGroup.MapPut("/{credentialId}", RenameAsync)
            .WithName("RenamePasskey")
            .WithTags("Passkeys")
            .RequireRateLimiting(PasskeyRateLimitPolicies.Management);
        mutationGroup.MapDelete("/{credentialId}", RemoveAsync)
            .WithName("RemovePasskey")
            .WithTags("Passkeys")
            .RequireRateLimiting(PasskeyRateLimitPolicies.Management);

        return endpoints;
    }

    private static IResult GetConfiguration(IOptions<PasskeyOptions> options)
    {
        var passkeys = options.Value;
        return Results.Ok(new PasskeyConfigurationResponse(
            passkeys.Enabled,
            Usernameless: true,
            ConditionalUi: true,
            passkeys.UserVerification,
            passkeys.Signup.EnabledModes.ToArray(),
            SignupEmailVerificationRequired: true));
    }

    private static async Task<IResult> MakeAuthenticationOptionsAsync(
        PasskeyOptionsRequest request,
        PasskeyClientValidator clientValidator,
        SignInManager<ApplicationUser> signInManager,
        CancellationToken cancellationToken)
    {
        if (!await clientValidator.IsValidPublicAuthorizationClientAsync(request.ClientId, cancellationToken))
        {
            return Problem("invalid_passkey_request", "Invalid passkey request.");
        }

        var json = await signInManager.MakePasskeyRequestOptionsAsync(null!);
        return Results.Content(json, "application/json");
    }

    private static async Task<IResult> AuthenticateAsync(
        PasskeyAuthenticationRequest request,
        PasskeyClientValidator clientValidator,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IAuditLogger auditLogger,
        CancellationToken cancellationToken)
    {
        if (!await clientValidator.IsValidPublicAuthorizationClientAsync(request.ClientId, cancellationToken) ||
            request.Credential.ValueKind != System.Text.Json.JsonValueKind.Object)
        {
            return Problem("invalid_passkey_request", "Invalid passkey request.");
        }

        var assertion = await signInManager.PerformPasskeyAssertionAsync(request.Credential.GetRawText());
        if (!assertion.Succeeded || assertion.User is null || assertion.Passkey is null)
        {
            await auditLogger.LogAnonymousAsync(
                AuditEventTypes.PasskeyAuthenticationFailed,
                new { request.ClientId },
                cancellationToken);
            return Problem("passkey_authentication_failed", "Passkey authentication failed.");
        }

        var user = assertion.User;
        if (!await signInManager.CanSignInAsync(user) ||
            await userManager.IsLockedOutAsync(user))
        {
            await auditLogger.LogAnonymousAsync(
                AuditEventTypes.PasskeyAuthenticationFailed,
                new { request.ClientId },
                cancellationToken);
            return Problem("passkey_authentication_failed", "Passkey authentication failed.");
        }

        var update = await userManager.AddOrUpdatePasskeyAsync(user, assertion.Passkey);
        if (!update.Succeeded)
        {
            return Problem("passkey_authentication_failed", "Passkey authentication failed.");
        }

        await userManager.ResetAccessFailedCountAsync(user);
        await signInManager.SignInAsync(user, isPersistent: false, authenticationMethod: "passkey");
        await auditLogger.LogAsync(
            AuditEventTypes.PasskeyAuthenticated,
            user.Id,
            new { request.ClientId },
            cancellationToken);

        return Results.Ok(new
        {
            message = "Login successful. Continue with authorization code flow.",
            clientId = request.ClientId,
            authenticationMethod = "passkey"
        });
    }

    private static async Task<IResult> ListAsync(
        HttpContext context,
        UserManager<ApplicationUser> userManager,
        PasskeyManagementService service)
    {
        var user = await ResolveUserAsync(context.User, userManager);
        return user is null
            ? Results.Unauthorized()
            : Results.Ok(await service.ListAsync(user));
    }

    private static async Task<IResult> MakeCreationOptionsAsync(
        HttpContext context,
        UserManager<ApplicationUser> userManager,
        PasskeyManagementService service)
    {
        var user = await ResolveUserAsync(context.User, userManager);
        if (user is null)
        {
            return Results.Unauthorized();
        }

        var json = await service.MakeCreationOptionsAsync(user);
        return json is null
            ? Problem("passkey_limit_reached", "Passkey limit reached.", StatusCodes.Status409Conflict)
            : Results.Content(json, "application/json");
    }

    private static async Task<IResult> CreateAsync(
        HttpContext context,
        PasskeyCreationRequest request,
        UserManager<ApplicationUser> userManager,
        PasskeyManagementService service,
        IAuditLogger auditLogger,
        IUserLifecycleHookDispatcher lifecycleDispatcher,
        CancellationToken cancellationToken)
    {
        var user = await ResolveUserAsync(context.User, userManager);
        if (user is null)
        {
            return Results.Unauthorized();
        }

        if (request.Credential.ValueKind != System.Text.Json.JsonValueKind.Object)
        {
            return Problem("invalid_passkey_request", "Invalid passkey request.");
        }

        var lifecycleContext = new UserLifecycleContext(
            UserLifecycleEvent.PasskeyRegistered,
            user,
            Source: nameof(CreateAsync));
        try
        {
            await lifecycleDispatcher.EnsureCanRegisterPasskeyAsync(lifecycleContext, cancellationToken);
        }
        catch (LifecycleHookRejectedException exception)
        {
            return Problem("passkey_registration_rejected", exception.Message);
        }

        var result = await service.AddAsync(
            user,
            request.Name,
            request.Credential.GetRawText(),
            cancellationToken);
        if (result.Status == PasskeyMutationStatus.Success)
        {
            await auditLogger.LogAsync(
                AuditEventTypes.PasskeyRegistered,
                user.Id,
                new { PasskeyCount = (await userManager.GetPasskeysAsync(user)).Count },
                cancellationToken);
            await lifecycleDispatcher.NotifyPasskeyRegisteredAsync(lifecycleContext, cancellationToken);
            return Results.Ok(result.Passkey);
        }

        return MutationProblem(result.Status);
    }

    private static async Task<IResult> RenameAsync(
        HttpContext context,
        string credentialId,
        PasskeyRenameRequest request,
        UserManager<ApplicationUser> userManager,
        PasskeyManagementService service,
        IAuditLogger auditLogger,
        IUserLifecycleHookDispatcher lifecycleDispatcher,
        CancellationToken cancellationToken)
    {
        var user = await ResolveUserAsync(context.User, userManager);
        if (user is null)
        {
            return Results.Unauthorized();
        }

        if (!PasskeyEncoding.TryCredentialId(credentialId, out var decoded))
        {
            return Results.NotFound();
        }

        var lifecycleContext = new UserLifecycleContext(
            UserLifecycleEvent.PasskeyRenamed,
            user,
            Source: nameof(RenameAsync));
        try
        {
            await lifecycleDispatcher.EnsureCanRenamePasskeyAsync(lifecycleContext, cancellationToken);
        }
        catch (LifecycleHookRejectedException exception)
        {
            return Problem("passkey_rename_rejected", exception.Message);
        }

        var result = await service.RenameAsync(
            user,
            decoded,
            request.Name,
            request.ConcurrencyStamp,
            cancellationToken);
        if (result.Status == PasskeyMutationStatus.Success)
        {
            await auditLogger.LogAsync(AuditEventTypes.PasskeyRenamed, user.Id, cancellationToken: cancellationToken);
            await lifecycleDispatcher.NotifyPasskeyRenamedAsync(lifecycleContext, cancellationToken);
            return Results.Ok(result.Passkey);
        }

        return MutationProblem(result.Status);
    }

    private static async Task<IResult> RemoveAsync(
        HttpContext context,
        string credentialId,
        UserManager<ApplicationUser> userManager,
        PasskeyManagementService service,
        IAuditLogger auditLogger,
        IUserLifecycleHookDispatcher lifecycleDispatcher,
        CancellationToken cancellationToken)
    {
        var user = await ResolveUserAsync(context.User, userManager);
        if (user is null)
        {
            return Results.Unauthorized();
        }

        if (!PasskeyEncoding.TryCredentialId(credentialId, out var decoded))
        {
            return Results.NotFound();
        }

        var lifecycleContext = new UserLifecycleContext(
            UserLifecycleEvent.PasskeyRemoved,
            user,
            Source: nameof(RemoveAsync));
        try
        {
            await lifecycleDispatcher.EnsureCanRemovePasskeyAsync(lifecycleContext, cancellationToken);
        }
        catch (LifecycleHookRejectedException exception)
        {
            return Problem("passkey_removal_rejected", exception.Message);
        }

        var result = await service.RemoveAsync(user, decoded, cancellationToken);
        if (result.Status is PasskeyMutationStatus.Success or PasskeyMutationStatus.NotFound)
        {
            if (result.Status == PasskeyMutationStatus.Success)
            {
                await auditLogger.LogAsync(AuditEventTypes.PasskeyRemoved, user.Id, cancellationToken: cancellationToken);
                await lifecycleDispatcher.NotifyPasskeyRemovedAsync(lifecycleContext, cancellationToken);
            }

            return Results.NoContent();
        }

        return MutationProblem(result.Status);
    }

    internal static async Task<ApplicationUser?> ResolveUserAsync(
        ClaimsPrincipal principal,
        UserManager<ApplicationUser> userManager)
    {
        var id = principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? principal.FindFirstValue(OpenIddictConstants.Claims.Subject);
        return Guid.TryParse(id, out var userId)
            ? await userManager.FindByIdAsync(userId.ToString())
            : null;
    }

    internal static IResult Problem(string title, string detail, int status = StatusCodes.Status400BadRequest)
        => Results.Problem(statusCode: status, title: title, detail: detail);

    private static IResult MutationProblem(PasskeyMutationStatus status)
        => status switch
        {
            PasskeyMutationStatus.InvalidName =>
                Problem("invalid_passkey_request", "Invalid passkey name."),
            PasskeyMutationStatus.LimitReached =>
                Problem("passkey_limit_reached", "Passkey limit reached.", StatusCodes.Status409Conflict),
            PasskeyMutationStatus.NotFound => Results.NotFound(),
            PasskeyMutationStatus.Modified =>
                Problem("passkey_modified", "Passkey was modified.", StatusCodes.Status409Conflict),
            PasskeyMutationStatus.LoginMethodRequired =>
                Problem("login_method_required", "At least one login method is required.", StatusCodes.Status409Conflict),
            _ => Problem("passkey_registration_failed", "Passkey registration failed.")
        };
}
