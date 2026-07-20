namespace Identity.Base.OpenIddict;

/// <summary>Identifies client-credentials clients managed by an opt-in package.</summary>
public interface IManagedClientCredentialsClientResolver
{
    Task<bool> IsManagedAsync(string clientId, CancellationToken cancellationToken = default);
}
