namespace Identity.Base.ServicePrincipals.Domain;

public sealed class ServicePrincipalCredential
{
    private ServicePrincipalCredential() { }

    public ServicePrincipalCredential(Guid servicePrincipalId, string name, string secretHash, DateTimeOffset? expiresAt)
    {
        ServicePrincipalId = servicePrincipalId;
        Name = name;
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

        RevokedAt = DateTimeOffset.UtcNow;
        RevokedReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
    }
}
