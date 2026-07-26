namespace Identity.Base.ServicePrincipals.Domain;

public sealed class ServicePrincipalCredential
{
    public const int MaxNameLength = 128;
    public const int MaxRevokedReasonLength = 256;

    private ServicePrincipalCredential() { }

    public ServicePrincipalCredential(Guid servicePrincipalId, string name, string secretHash, DateTimeOffset? expiresAt)
    {
        ServicePrincipalId = servicePrincipalId;
        Name = NormalizeName(name);
        SecretHash = secretHash;
        ExpiresAt = expiresAt;
    }

    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid ServicePrincipalId { get; private set; }
    public string Name { get; private set; } = null!;
    public string SecretHash { get; private set; } = null!;
    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ExpiresAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }
    public string? RevokedReason { get; private set; }
    public ServicePrincipal ServicePrincipal { get; private set; } = null!;

    public bool IsActive(DateTimeOffset now) =>
        RevokedAt is null && (ExpiresAt is null || ExpiresAt > now);

    public void Revoke(string? reason)
    {
        if (RevokedAt is not null)
        {
            return;
        }

        var normalizedReason = NormalizeRevokedReason(reason);
        RevokedAt = DateTimeOffset.UtcNow;
        RevokedReason = normalizedReason;
    }

    internal void SetSecretHash(string secretHash) => SecretHash = secretHash;

    internal static string NormalizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Credential name is required.", nameof(name));
        }

        var normalized = name.Trim();
        return normalized.Length <= MaxNameLength
            ? normalized
            : throw new ArgumentException(
                $"Credential name cannot exceed {MaxNameLength} characters.",
                nameof(name));
    }

    internal static string? NormalizeRevokedReason(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return null;
        }

        var normalized = reason.Trim();
        return normalized.Length <= MaxRevokedReasonLength
            ? normalized
            : throw new ArgumentException(
                $"Revocation reason cannot exceed {MaxRevokedReasonLength} characters.",
                nameof(reason));
    }
}
