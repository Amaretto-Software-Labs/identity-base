using Microsoft.Extensions.Logging;

namespace Identity.Base.Logging;

public interface IAuditLogger
{
    Task LogAsync(string eventType, Guid userId, object? details = null, CancellationToken cancellationToken = default);

    Task LogAnonymousAsync(string eventType, object? details = null, CancellationToken cancellationToken = default);
}

public static class AuditEventTypes
{
    public const string MfaEnabled = "mfa.enabled";
    public const string MfaDisabled = "mfa.disabled";
    public const string MfaChallengeSent = "mfa.challenge.sent";
    public const string MfaRecoveryCodesRegenerated = "mfa.recovery.regenerated";
    public const string MfaVerified = "mfa.verified";
    public const string ProfileUpdated = "profile.updated";
    public const string ExternalLinked = "external.linked";
    public const string ExternalUnlinked = "external.unlinked";
    public const string ExternalLogin = "external.login";
    public const string AdminUserCreated = "admin.user.created";
    public const string AdminUserUpdated = "admin.user.updated";
    public const string AdminUserLocked = "admin.user.locked";
    public const string AdminUserUnlocked = "admin.user.unlocked";
    public const string AdminUserPasswordReset = "admin.user.password-reset";
    public const string AdminUserMfaReset = "admin.user.mfa-reset";
    public const string AdminUserConfirmationResent = "admin.user.confirmation-resent";
    public const string AdminUserDeleted = "admin.user.deleted";
    public const string AdminUserRestored = "admin.user.restored";
    public const string AdminUserRolesUpdated = "admin.user.roles-updated";
    public const string AdminRoleCreated = "admin.role.created";
    public const string AdminRoleUpdated = "admin.role.updated";
    public const string AdminRoleDeleted = "admin.role.deleted";
}

internal sealed class AuditLogger : IAuditLogger
{
    private readonly ILogger<AuditLogger> _logger;

    public AuditLogger(ILogger<AuditLogger> logger)
    {
        _logger = logger;
    }

    public Task LogAsync(string eventType, Guid userId, object? details = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _logger.LogInformation("Audit event {EventType} for user {UserId} {@Details}", eventType, userId, details);
        return Task.CompletedTask;
    }

    public Task LogAnonymousAsync(string eventType, object? details = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _logger.LogInformation("Audit event {EventType} for anonymous {@Details}", eventType, details);
        return Task.CompletedTask;
    }
}
