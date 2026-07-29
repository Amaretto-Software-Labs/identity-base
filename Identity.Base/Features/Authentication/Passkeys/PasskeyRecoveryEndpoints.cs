using System.Data;
using System.Security.Cryptography;
using Identity.Base.Data;
using Identity.Base.Identity;
using Identity.Base.Logging;
using Identity.Base.Lifecycle;
using Identity.Base.Options;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Identity.Base.Features.Authentication.Passkeys;

internal static class PasskeyRecoveryEndpoints
{
    private const string RecoveryMode = "recovery";

    public static RouteGroupBuilder MapPasskeyRecoveryEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/passkeys/recovery/begin", BeginAsync)
            .WithName("BeginPasskeyRecovery")
            .WithTags("Passkeys")
            .RequireRateLimiting(PasskeyRateLimitPolicies.RecoveryEmail);
        group.MapPost("/passkeys/recovery/confirm-email", ConfirmEmailAsync)
            .WithName("ConfirmPasskeyRecoveryEmail")
            .WithTags("Passkeys")
            .RequireRateLimiting(PasskeyRateLimitPolicies.RecoveryEnrollment);
        group.MapPost("/passkeys/recovery/creation/options", MakeCreationOptionsAsync)
            .WithName("MakePasskeyRecoveryCreationOptions")
            .WithTags("Passkeys")
            .RequireRateLimiting(PasskeyRateLimitPolicies.RecoveryEnrollment);
        group.MapPost("/passkeys/recovery/complete", CompleteAsync)
            .WithName("CompletePasskeyRecovery")
            .WithTags("Passkeys")
            .WithMetadata(new RequestSizeLimitAttribute(64 * 1024))
            .RequireRateLimiting(PasskeyRateLimitPolicies.RecoveryEnrollment);
        return group;
    }

    private static async Task<IResult> BeginAsync(
        PasskeyRecoveryBeginRequest request,
        PasskeyClientValidator clientValidator,
        AppDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        PasskeyEmailService emailService,
        IOptions<PasskeyOptions> passkeyOptions,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        if (!await clientValidator.IsValidPublicAuthorizationClientAsync(request.ClientId, cancellationToken))
        {
            return PasskeyEndpoints.Problem("invalid_passkey_request", "Invalid passkey request.");
        }

        var correlationId = Guid.NewGuid().ToString("N");
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null ||
            !await signInManager.CanSignInAsync(user) ||
            await userManager.IsLockedOutAsync(user) ||
            await userManager.HasPasswordAsync(user) ||
            (await userManager.GetLoginsAsync(user)).Count > 0 ||
            string.IsNullOrWhiteSpace(passkeyOptions.Value.Recovery.ConfirmationUrlTemplate))
        {
            return Results.Accepted($"/auth/passkeys/recovery/{correlationId}", new { correlationId });
        }

        var token = RandomNumberGenerator.GetBytes(32);
        var now = DateTimeOffset.UtcNow;
        var draft = new PasskeyRecoveryDraft
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            ClientId = request.ClientId,
            ConfirmationTokenHash = SHA256.HashData(token),
            CreatedAt = now,
            ExpiresAt = now.AddMinutes(passkeyOptions.Value.Recovery.DraftLifetimeMinutes)
        };

        var existing = await dbContext.PasskeyRecoveryDrafts
            .Where(candidate => candidate.UserId == user.Id)
            .ToListAsync(cancellationToken);
        dbContext.PasskeyRecoveryDrafts.RemoveRange(existing);
        dbContext.PasskeyRecoveryDrafts.Add(draft);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
        {
            loggerFactory.CreateLogger(typeof(PasskeyRecoveryEndpoints).FullName!)
                .LogWarning(exception, "A concurrent passkey recovery draft superseded draft {DraftId}.", draft.Id);
            return Results.Accepted($"/auth/passkeys/recovery/{correlationId}", new { correlationId });
        }

        var confirmationUrl = BuildConfirmationUrl(
            passkeyOptions.Value.Recovery.ConfirmationUrlTemplate,
            draft.Id,
            PasskeyEncoding.Token(token));
        try
        {
            await emailService.SendRecoveryConfirmationAsync(user, confirmationUrl, cancellationToken);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            loggerFactory.CreateLogger(typeof(PasskeyRecoveryEndpoints).FullName!)
                .LogError(exception, "Failed to send passkey recovery confirmation for draft {DraftId}.", draft.Id);
        }

        return Results.Accepted($"/auth/passkeys/recovery/{correlationId}", new { correlationId });
    }

    private static async Task<IResult> ConfirmEmailAsync(
        HttpContext context,
        PasskeyEmailConfirmationRequest request,
        AppDbContext dbContext,
        PasskeyStateProtector stateProtector,
        CancellationToken cancellationToken)
    {
        var draft = await dbContext.PasskeyRecoveryDrafts
            .SingleOrDefaultAsync(candidate => candidate.Id == request.DraftId, cancellationToken);
        if (draft is null ||
            draft.ExpiresAt <= DateTimeOffset.UtcNow ||
            draft.ConsumedAt is not null ||
            !PasskeyEncoding.TryToken(request.Token, out var token) ||
            !CryptographicOperations.FixedTimeEquals(
                draft.ConfirmationTokenHash,
                SHA256.HashData(token)))
        {
            return RecoveryFailed();
        }

        draft.EmailConfirmedAt ??= DateTimeOffset.UtcNow;
        draft.ConcurrencyStamp = Guid.NewGuid().ToString("N");
        await dbContext.SaveChangesAsync(cancellationToken);
        stateProtector.WriteRecovery(
            context.Response,
            new PasskeyDraftState(
                draft.Id,
                draft.UserId,
                RecoveryMode,
                draft.ClientId,
                draft.ExpiresAt));
        return Results.NoContent();
    }

    private static async Task<IResult> MakeCreationOptionsAsync(
        HttpContext context,
        AppDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        PasskeyStateProtector stateProtector,
        CancellationToken cancellationToken)
    {
        var state = stateProtector.ReadRecovery(context.Request);
        var draft = state is null
            ? null
            : await dbContext.PasskeyRecoveryDrafts
                .AsNoTracking()
                .SingleOrDefaultAsync(candidate => candidate.Id == state.DraftId, cancellationToken);
        if (!IsUsable(draft, state))
        {
            return RecoveryFailed();
        }

        var user = await userManager.FindByIdAsync(draft!.UserId.ToString());
        if (user is null ||
            !await signInManager.CanSignInAsync(user) ||
            await userManager.IsLockedOutAsync(user) ||
            await userManager.HasPasswordAsync(user) ||
            (await userManager.GetLoginsAsync(user)).Count > 0)
        {
            return RecoveryFailed();
        }

        var json = await signInManager.MakePasskeyCreationOptionsAsync(new PasskeyUserEntity
        {
            Id = user.Id.ToString(),
            Name = user.Email ?? user.UserName ?? "User",
            DisplayName = user.DisplayName ?? user.Email ?? user.UserName ?? "User"
        });
        return Results.Content(json, "application/json");
    }

    private static async Task<IResult> CompleteAsync(
        HttpContext context,
        PasskeyCreationRequest request,
        AppDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        PasskeyStateProtector stateProtector,
        PasskeyEmailService emailService,
        IOptions<PasskeyOptions> passkeyOptions,
        IAuditLogger auditLogger,
        IUserLifecycleHookDispatcher lifecycleDispatcher,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var state = stateProtector.ReadRecovery(context.Request);
        var draft = state is null
            ? null
            : await dbContext.PasskeyRecoveryDrafts
                .SingleOrDefaultAsync(candidate => candidate.Id == state.DraftId, cancellationToken);
        if (!IsUsable(draft, state) ||
            request.Credential.ValueKind != System.Text.Json.JsonValueKind.Object)
        {
            return RecoveryFailed();
        }

        var name = request.Name.Trim();
        if (name.Length == 0 || name.Length > passkeyOptions.Value.NameMaxLength)
        {
            return RecoveryFailed();
        }

        var user = await userManager.FindByIdAsync(draft!.UserId.ToString());
        if (user is null ||
            !await signInManager.CanSignInAsync(user) ||
            await userManager.IsLockedOutAsync(user) ||
            await userManager.HasPasswordAsync(user) ||
            (await userManager.GetLoginsAsync(user)).Count > 0)
        {
            return RecoveryFailed();
        }

        var lifecycleContext = new UserLifecycleContext(
            UserLifecycleEvent.PasskeyRecoveryCompleted,
            user,
            Source: nameof(PasskeyRecoveryEndpoints));
        try
        {
            await lifecycleDispatcher.EnsureCanCompletePasskeyRecoveryAsync(lifecycleContext, cancellationToken);
        }
        catch (LifecycleHookRejectedException)
        {
            return RecoveryFailed();
        }

        var attestation = await signInManager.PerformPasskeyAttestationAsync(request.Credential.GetRawText());
        if (!attestation.Succeeded ||
            attestation.Passkey is null ||
            attestation.UserEntity is null ||
            !string.Equals(attestation.UserEntity.Id, user.Id.ToString(), StringComparison.Ordinal))
        {
            return RecoveryFailed();
        }

        attestation.Passkey.Name = name;
        var oldPasskeys = (await userManager.GetPasskeysAsync(user))
            .Select(passkey => passkey.CredentialId)
            .ToArray();

        await using var transaction = dbContext.Database.IsRelational()
            ? await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
            : null;

        var addResult = await userManager.AddOrUpdatePasskeyAsync(user, attestation.Passkey);
        if (!addResult.Succeeded)
        {
            return RecoveryFailed();
        }

        foreach (var credentialId in oldPasskeys)
        {
            var removeResult = await userManager.RemovePasskeyAsync(user, credentialId);
            if (!removeResult.Succeeded)
            {
                return RecoveryFailed();
            }
        }

        var stampResult = await userManager.UpdateSecurityStampAsync(user);
        if (!stampResult.Succeeded)
        {
            return RecoveryFailed();
        }

        draft.ConsumedAt = DateTimeOffset.UtcNow;
        draft.ConcurrencyStamp = Guid.NewGuid().ToString("N");
        await dbContext.SaveChangesAsync(cancellationToken);
        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        stateProtector.ClearRecovery(context.Response);
        await signInManager.SignInAsync(user, isPersistent: false, authenticationMethod: "passkey");
        await auditLogger.LogAsync(
            AuditEventTypes.PasskeyRecoveryCompleted,
            user.Id,
            new { draft.ClientId, RevokedPasskeyCount = oldPasskeys.Length },
            cancellationToken);
        await lifecycleDispatcher.NotifyPasskeyRecoveryCompletedAsync(lifecycleContext, cancellationToken);
        try
        {
            await emailService.SendRecoveryCompletedAsync(user, cancellationToken);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            loggerFactory.CreateLogger(typeof(PasskeyRecoveryEndpoints).FullName!)
                .LogError(exception, "Failed to send passkey recovery completion notice for user {UserId}.", user.Id);
        }

        return Results.Ok(new
        {
            message = "Recovery successful. Continue with authorization code flow.",
            clientId = draft.ClientId,
            authenticationMethod = "passkey",
            recovered = true
        });
    }

    private static bool IsUsable(PasskeyRecoveryDraft? draft, PasskeyDraftState? state)
        => draft is not null &&
           state is not null &&
           draft.Id == state.DraftId &&
           draft.UserId == state.UserId &&
           string.Equals(state.Mode, RecoveryMode, StringComparison.Ordinal) &&
           string.Equals(draft.ClientId, state.ClientId, StringComparison.Ordinal) &&
           draft.ExpiresAt == state.ExpiresAt &&
           draft.EmailConfirmedAt is not null &&
           draft.ConsumedAt is null &&
           draft.ExpiresAt > DateTimeOffset.UtcNow;

    private static IResult RecoveryFailed()
        => PasskeyEndpoints.Problem("passkey_recovery_failed", "Passkey recovery failed.");

    private static string BuildConfirmationUrl(string template, Guid draftId, string token)
        => template
            .Replace("{draftId}", Uri.EscapeDataString(draftId.ToString()), StringComparison.Ordinal)
            .Replace("{token}", Uri.EscapeDataString(token), StringComparison.Ordinal);
}
