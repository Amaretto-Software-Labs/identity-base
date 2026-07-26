namespace Identity.Base.ServicePrincipals.Domain;

public sealed class ServicePrincipal
{
    public const int MaxDisplayNameLength = 200;
    public const int MaxClientIdLength = 200;

    private ServicePrincipal() { }

    public ServicePrincipal(string displayName, string clientId)
    {
        DisplayName = NormalizeDisplayName(displayName);
        ClientId = NormalizeClientId(clientId);
    }

    public Guid Id { get; private set; } = Guid.NewGuid();
    public string DisplayName { get; private set; } = null!;
    public string ClientId { get; private set; } = null!;
    public bool IsDisabled { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; private set; } = DateTimeOffset.UtcNow;
    public string ConcurrencyStamp { get; private set; } = Guid.NewGuid().ToString("N");
    public ICollection<ServicePrincipalCredential> Credentials { get; } = new List<ServicePrincipalCredential>();

    public void UpdateDisplayName(string displayName)
    {
        DisplayName = NormalizeDisplayName(displayName);
        Touch();
    }

    public void Disable()
    {
        IsDisabled = true;
        Touch();
    }

    public void Restore()
    {
        IsDisabled = false;
        Touch();
    }

    private void Touch()
    {
        UpdatedAt = DateTimeOffset.UtcNow;
        ConcurrencyStamp = Guid.NewGuid().ToString("N");
    }

    internal static string NormalizeDisplayName(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new ArgumentException("Display name is required.", nameof(displayName));
        }

        var normalized = displayName.Trim();
        return normalized.Length <= MaxDisplayNameLength
            ? normalized
            : throw new ArgumentException(
                $"Display name cannot exceed {MaxDisplayNameLength} characters.",
                nameof(displayName));
    }

    internal static string NormalizeClientId(string clientId)
    {
        if (string.IsNullOrWhiteSpace(clientId))
        {
            throw new ArgumentException("Client ID is required.", nameof(clientId));
        }

        var normalized = clientId.Trim();
        return normalized.Length <= MaxClientIdLength
            ? normalized
            : throw new ArgumentException(
                $"Client ID cannot exceed {MaxClientIdLength} characters.",
                nameof(clientId));
    }
}
