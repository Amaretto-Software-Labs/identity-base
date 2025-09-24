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
}

public sealed class AuditLogger : IAuditLogger
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
