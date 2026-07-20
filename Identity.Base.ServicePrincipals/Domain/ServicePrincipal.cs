namespace Identity.Base.ServicePrincipals.Domain;

public sealed class ServicePrincipal
{
    private ServicePrincipal() { }

    public ServicePrincipal(string displayName, string clientId)
    {
        DisplayName = displayName;
        ClientId = clientId;
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
        DisplayName = displayName;
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
}
