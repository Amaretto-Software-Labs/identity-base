using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Identity.Base.Data;
using Identity.Base.Extensions;
using Identity.Base.Identity;
using Identity.Base.Lifecycle;
using Identity.Base.Logging;
using Identity.Base.Options;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Identity.Base.Features.Authentication.Passkeys;

internal static class PasskeySignupEndpoints
{
    public static RouteGroupBuilder MapPasskeySignupEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/passkeys/registration/begin", BeginAsync)
            .WithName("BeginPasskeySignup")
            .WithTags("Passkeys")
            .RequireRateLimiting(PasskeyRateLimitPolicies.SignupEmail);
        group.MapPost("/passkeys/registration/resend", ResendAsync)
            .WithName("ResendPasskeySignupConfirmation")
            .WithTags("Passkeys")
            .RequireRateLimiting(PasskeyRateLimitPolicies.SignupEmail);
        group.MapPost("/passkeys/registration/confirm-email", ConfirmEmailAsync)
            .WithName("ConfirmPasskeySignupEmail")
            .WithTags("Passkeys")
            .RequireRateLimiting(PasskeyRateLimitPolicies.SignupEnrollment);
        group.MapPost("/passkeys/registration/creation/options", MakeCreationOptionsAsync)
            .WithName("MakePasskeySignupCreationOptions")
            .WithTags("Passkeys")
            .RequireRateLimiting(PasskeyRateLimitPolicies.SignupEnrollment);
        group.MapPost("/passkeys/registration/complete", CompleteAsync)
            .WithName("CompletePasskeySignup")
            .WithTags("Passkeys")
            .WithMetadata(new RequestSizeLimitAttribute(64 * 1024))
            .RequireRateLimiting(PasskeyRateLimitPolicies.SignupEnrollment);
        return group;
    }

    private static async Task<IResult> BeginAsync(
        PasskeySignupBeginRequest request,
        PasskeyClientValidator clientValidator,
        AppDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        PasskeyEmailService emailService,
        IOptions<PasskeyOptions> passkeyOptions,
        IOptions<RegistrationOptions> registrationOptions,
        IAuditLogger auditLogger,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var options = passkeyOptions.Value;
        if (!options.Signup.EnabledModes.Contains(request.Mode, StringComparer.Ordinal))
        {
            return PasskeyEndpoints.Problem(
                "unsupported_registration_mode",
                "The requested registration mode is not enabled.");
        }

        if (!await clientValidator.IsValidPublicAuthorizationClientAsync(request.ClientId, cancellationToken))
        {
            return PasskeyEndpoints.Problem("invalid_passkey_request", "Invalid passkey request.");
        }

        var validationErrors = ValidateSignupInput(request, registrationOptions.Value);
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var correlationId = Guid.NewGuid().ToString("N");
        var normalizedEmail = userManager.NormalizeEmail(request.Email);
        if (string.IsNullOrWhiteSpace(normalizedEmail) ||
            await userManager.FindByEmailAsync(request.Email) is not null)
        {
            return Results.Accepted($"/auth/passkeys/registration/{correlationId}", new { correlationId });
        }

        var token = RandomNumberGenerator.GetBytes(32);
        var now = DateTimeOffset.UtcNow;
        var metadata = request.Metadata ??
            new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        var proposedUser = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = request.Email.Trim(),
            UserName = request.Email.Trim(),
            DisplayName = IdentityDisplayNameResolver.Resolve(
                new Dictionary<string, string?>(metadata, StringComparer.OrdinalIgnoreCase),
                registrationOptions.Value.ProfileFields)
        };
        proposedUser.SetProfileMetadata(metadata);

        var draft = new PasskeyRegistrationDraft
        {
            Id = Guid.NewGuid(),
            ReservedUserId = proposedUser.Id,
            Email = proposedUser.Email,
            NormalizedEmail = normalizedEmail,
            Mode = request.Mode,
            ClientId = request.ClientId,
            ProfileMetadataJson = proposedUser.ProfileMetadata.ToJson(),
            DisplayName = proposedUser.DisplayName,
            ConfirmationTokenHash = SHA256.HashData(token),
            CreatedAt = now,
            ExpiresAt = now.AddMinutes(options.Signup.DraftLifetimeMinutes)
        };

        var existingDrafts = await dbContext.PasskeyRegistrationDrafts
            .Where(existing => existing.NormalizedEmail == normalizedEmail)
            .ToListAsync(cancellationToken);
        dbContext.PasskeyRegistrationDrafts.RemoveRange(existingDrafts);
        dbContext.PasskeyRegistrationDrafts.Add(draft);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
        {
            loggerFactory.CreateLogger(typeof(PasskeySignupEndpoints).FullName!)
                .LogWarning(exception, "A concurrent passkey signup draft superseded draft {DraftId}.", draft.Id);
            return Results.Accepted($"/auth/passkeys/registration/{correlationId}", new { correlationId });
        }

        var confirmationUrl = BuildConfirmationUrl(
            options.Signup.ConfirmationUrlTemplate,
            draft.Id,
            PasskeyEncoding.Token(token));
        try
        {
            await emailService.SendSignupConfirmationAsync(
                proposedUser,
                confirmationUrl,
                request.Mode,
                cancellationToken);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            loggerFactory.CreateLogger(typeof(PasskeySignupEndpoints).FullName!)
                .LogError(exception, "Failed to send passkey signup confirmation for draft {DraftId}.", draft.Id);
        }

        await auditLogger.LogAnonymousAsync(
            AuditEventTypes.PasskeySignupStarted,
            new { request.Mode, request.ClientId, CorrelationId = correlationId },
            cancellationToken);
        return Results.Accepted($"/auth/passkeys/registration/{correlationId}", new { correlationId });
    }

    private static async Task<IResult> ResendAsync(
        PasskeyDraftRequest request,
        AppDbContext dbContext,
        PasskeyEmailService emailService,
        IOptions<PasskeyOptions> passkeyOptions,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var correlationId = Guid.NewGuid().ToString("N");
        var draft = await dbContext.PasskeyRegistrationDrafts
            .SingleOrDefaultAsync(candidate => candidate.Id == request.DraftId, cancellationToken);
        if (draft is null ||
            draft.ExpiresAt <= DateTimeOffset.UtcNow ||
            draft.ConsumedAt is not null)
        {
            return Results.Accepted($"/auth/passkeys/registration/{correlationId}", new { correlationId });
        }

        var token = RandomNumberGenerator.GetBytes(32);
        draft.ConfirmationTokenHash = SHA256.HashData(token);
        draft.EmailConfirmedAt = null;
        draft.ConcurrencyStamp = Guid.NewGuid().ToString("N");
        await dbContext.SaveChangesAsync(cancellationToken);

        var user = new ApplicationUser
        {
            Id = draft.ReservedUserId,
            Email = draft.Email,
            UserName = draft.Email,
            DisplayName = draft.DisplayName
        };
        var confirmationUrl = BuildConfirmationUrl(
            passkeyOptions.Value.Signup.ConfirmationUrlTemplate,
            draft.Id,
            PasskeyEncoding.Token(token));
        try
        {
            await emailService.SendSignupConfirmationAsync(
                user,
                confirmationUrl,
                draft.Mode,
                cancellationToken);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            loggerFactory.CreateLogger(typeof(PasskeySignupEndpoints).FullName!)
                .LogError(exception, "Failed to resend passkey signup confirmation for draft {DraftId}.", draft.Id);
        }

        return Results.Accepted($"/auth/passkeys/registration/{correlationId}", new { correlationId });
    }

    private static async Task<IResult> ConfirmEmailAsync(
        HttpContext context,
        PasskeyEmailConfirmationRequest request,
        AppDbContext dbContext,
        PasskeyStateProtector stateProtector,
        IOptions<PasskeyOptions> passkeyOptions,
        CancellationToken cancellationToken)
    {
        var draft = await dbContext.PasskeyRegistrationDrafts
            .SingleOrDefaultAsync(candidate => candidate.Id == request.DraftId, cancellationToken);
        if (draft is null ||
            draft.ExpiresAt <= DateTimeOffset.UtcNow ||
            draft.ConsumedAt is not null ||
            !passkeyOptions.Value.Signup.EnabledModes.Contains(draft.Mode, StringComparer.Ordinal) ||
            !PasskeyEncoding.TryToken(request.Token, out var token) ||
            !CryptographicOperations.FixedTimeEquals(
                draft.ConfirmationTokenHash,
                SHA256.HashData(token)))
        {
            return PasskeyEndpoints.Problem(
                "passkey_registration_draft_invalid",
                "Registration link is invalid or expired.");
        }

        draft.EmailConfirmedAt ??= DateTimeOffset.UtcNow;
        draft.ConcurrencyStamp = Guid.NewGuid().ToString("N");
        await dbContext.SaveChangesAsync(cancellationToken);

        stateProtector.WriteRegistration(
            context.Response,
            new PasskeyDraftState(
                draft.Id,
                draft.ReservedUserId,
                draft.Mode,
                draft.ClientId,
                draft.ExpiresAt));
        return Results.NoContent();
    }

    private static async Task<IResult> MakeCreationOptionsAsync(
        HttpContext context,
        AppDbContext dbContext,
        PasskeyStateProtector stateProtector,
        SignInManager<ApplicationUser> signInManager,
        IOptions<PasskeyOptions> passkeyOptions,
        CancellationToken cancellationToken)
    {
        var state = stateProtector.ReadRegistration(context.Request);
        var draft = state is null
            ? null
            : await dbContext.PasskeyRegistrationDrafts
                .AsNoTracking()
                .SingleOrDefaultAsync(candidate => candidate.Id == state.DraftId, cancellationToken);
        if (!IsUsable(draft, state, passkeyOptions.Value))
        {
            return PasskeyEndpoints.Problem(
                "passkey_registration_draft_invalid",
                "Registration state is invalid or expired.");
        }

        var json = await signInManager.MakePasskeyCreationOptionsAsync(new PasskeyUserEntity
        {
            Id = draft!.ReservedUserId.ToString(),
            Name = draft.Email,
            DisplayName = draft.DisplayName ?? draft.Email
        });
        return Results.Content(json, "application/json");
    }

    private static async Task<IResult> CompleteAsync(
        HttpContext context,
        PasskeySignupCompleteRequest request,
        AppDbContext dbContext,
        PasskeyStateProtector stateProtector,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IOptions<PasskeyOptions> passkeyOptions,
        IUserLifecycleHookDispatcher lifecycleDispatcher,
        IAuditLogger auditLogger,
        CancellationToken cancellationToken)
    {
        var state = stateProtector.ReadRegistration(context.Request);
        var draft = state is null
            ? null
            : await dbContext.PasskeyRegistrationDrafts
                .SingleOrDefaultAsync(candidate => candidate.Id == state.DraftId, cancellationToken);
        if (!IsUsable(draft, state, passkeyOptions.Value) ||
            request.Credential.ValueKind != System.Text.Json.JsonValueKind.Object)
        {
            return PasskeyEndpoints.Problem(
                "passkey_registration_draft_invalid",
                "Registration state is invalid or expired.");
        }

        var name = request.Name.Trim();
        if (name.Length == 0 || name.Length > passkeyOptions.Value.NameMaxLength)
        {
            return PasskeyEndpoints.Problem("invalid_passkey_request", "Invalid passkey name.");
        }

        if (draft!.Mode == PasskeySignupModes.Assisted && string.IsNullOrWhiteSpace(request.Password))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["password"] = ["A password is required for passkey-assisted signup."]
            });
        }

        if (draft.Mode == PasskeySignupModes.Passwordless && request.Password is not null)
        {
            return PasskeyEndpoints.Problem(
                "invalid_passkey_request",
                "Password must be omitted for passwordless signup.");
        }

        var attestation = await signInManager.PerformPasskeyAttestationAsync(request.Credential.GetRawText());
        if (!attestation.Succeeded ||
            attestation.Passkey is null ||
            attestation.UserEntity is null ||
            !string.Equals(attestation.UserEntity.Id, draft.ReservedUserId.ToString(), StringComparison.Ordinal))
        {
            return PasskeyEndpoints.Problem(
                "passkey_registration_failed",
                "Passkey registration failed.");
        }

        var user = new ApplicationUser
        {
            Id = draft.ReservedUserId,
            Email = draft.Email,
            UserName = draft.Email,
            DisplayName = draft.DisplayName,
            EmailConfirmed = true
        };
        user.SetProfileMetadata(UserProfileMetadata.FromJson(draft.ProfileMetadataJson));

        var lifecycleContext = new UserLifecycleContext(
            UserLifecycleEvent.Registration,
            user,
            Source: nameof(PasskeySignupEndpoints),
            Items: new Dictionary<string, object?>
            {
                ["RegistrationMode"] = draft.Mode,
                ["Metadata"] = user.ProfileMetadata.Values
            });
        var passkeyLifecycleContext = new UserLifecycleContext(
            UserLifecycleEvent.PasskeyRegistered,
            user,
            Source: nameof(PasskeySignupEndpoints),
            Items: new Dictionary<string, object?>
            {
                ["RegistrationMode"] = draft.Mode
            });
        try
        {
            await lifecycleDispatcher.EnsureCanRegisterAsync(lifecycleContext, cancellationToken);
            await lifecycleDispatcher.EnsureCanRegisterPasskeyAsync(passkeyLifecycleContext, cancellationToken);
        }
        catch (LifecycleHookRejectedException exception)
        {
            return Results.Problem(exception.Message, statusCode: StatusCodes.Status400BadRequest);
        }

        await using var transaction = dbContext.Database.IsRelational()
            ? await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
            : null;

        if (await userManager.FindByEmailAsync(draft.Email) is not null)
        {
            return PasskeyEndpoints.Problem(
                "passkey_registration_failed",
                "Passkey registration failed.");
        }

        var createResult = draft.Mode == PasskeySignupModes.Assisted
            ? await userManager.CreateAsync(user, request.Password!)
            : await userManager.CreateAsync(user);
        if (!createResult.Succeeded)
        {
            return Results.ValidationProblem(createResult.ToDictionary());
        }

        attestation.Passkey.Name = name;
        var addResult = await userManager.AddOrUpdatePasskeyAsync(user, attestation.Passkey);
        if (!addResult.Succeeded)
        {
            if (transaction is null)
            {
                await userManager.DeleteAsync(user);
            }

            return PasskeyEndpoints.Problem(
                "passkey_registration_failed",
                "Passkey registration failed.");
        }

        draft.ConsumedAt = DateTimeOffset.UtcNow;
        draft.ConcurrencyStamp = Guid.NewGuid().ToString("N");
        await dbContext.SaveChangesAsync(cancellationToken);
        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        stateProtector.ClearRegistration(context.Response);
        await signInManager.SignInAsync(user, isPersistent: false, authenticationMethod: "passkey");
        await lifecycleDispatcher.NotifyUserRegisteredAsync(lifecycleContext, cancellationToken);
        await lifecycleDispatcher.NotifyPasskeyRegisteredAsync(passkeyLifecycleContext, cancellationToken);
        await auditLogger.LogAsync(
            AuditEventTypes.PasskeySignupCompleted,
            user.Id,
            new { RegistrationMode = draft.Mode, draft.ClientId },
            cancellationToken);

        return Results.Ok(new
        {
            message = "Registration successful. Continue with authorization code flow.",
            clientId = draft.ClientId,
            authenticationMethod = "passkey",
            registrationMode = draft.Mode
        });
    }

    private static bool IsUsable(
        PasskeyRegistrationDraft? draft,
        PasskeyDraftState? state,
        PasskeyOptions options)
        => draft is not null &&
           state is not null &&
           draft.Id == state.DraftId &&
           draft.ReservedUserId == state.UserId &&
           string.Equals(draft.Mode, state.Mode, StringComparison.Ordinal) &&
           string.Equals(draft.ClientId, state.ClientId, StringComparison.Ordinal) &&
           draft.ExpiresAt == state.ExpiresAt &&
           draft.EmailConfirmedAt is not null &&
           draft.ConsumedAt is null &&
           draft.ExpiresAt > DateTimeOffset.UtcNow &&
           options.Signup.EnabledModes.Contains(draft.Mode, StringComparer.Ordinal);

    private static Dictionary<string, string[]> ValidateSignupInput(
        PasskeySignupBeginRequest request,
        RegistrationOptions registration)
    {
        var failures = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        void Add(string key, string message)
        {
            if (!failures.TryGetValue(key, out var messages))
            {
                messages = [];
                failures[key] = messages;
            }

            messages.Add(message);
        }

        if (!new EmailAddressAttribute().IsValid(request.Email))
        {
            Add("email", "A valid email is required.");
        }

        var metadata = request.Metadata ??
            new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        var allowed = registration.ProfileFields.ToDictionary(
            field => field.Name,
            StringComparer.OrdinalIgnoreCase);
        foreach (var key in metadata.Keys.Where(key => !allowed.ContainsKey(key)))
        {
            Add($"metadata.{key}", "Unknown profile field.");
        }

        foreach (var field in allowed.Values)
        {
            metadata.TryGetValue(field.Name, out var value);
            value = value?.Trim();
            if (field.Required && string.IsNullOrWhiteSpace(value))
            {
                Add($"metadata.{field.Name}", "Field is required.");
            }
            else if (value?.Length > field.MaxLength)
            {
                Add($"metadata.{field.Name}", $"Field exceeds maximum length of {field.MaxLength} characters.");
            }
            else if (!string.IsNullOrWhiteSpace(value) &&
                     !string.IsNullOrWhiteSpace(field.Pattern) &&
                     !Regex.IsMatch(value, field.Pattern, RegexOptions.None, TimeSpan.FromMilliseconds(250)))
            {
                Add($"metadata.{field.Name}", "Field does not match the required pattern.");
            }
        }

        return failures.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray(), StringComparer.OrdinalIgnoreCase);
    }

    private static string BuildConfirmationUrl(string template, Guid draftId, string token)
        => template
            .Replace("{draftId}", Uri.EscapeDataString(draftId.ToString()), StringComparison.Ordinal)
            .Replace("{token}", Uri.EscapeDataString(token), StringComparison.Ordinal);
}
