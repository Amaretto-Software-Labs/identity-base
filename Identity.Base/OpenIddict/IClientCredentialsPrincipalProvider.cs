using System.Security.Claims;

namespace Identity.Base.OpenIddict;

/// <summary>
/// Allows opt-in packages to create a principal for a managed client credentials client.
/// Returning <see langword="null"/> preserves the legacy configuration-backed client behavior.
/// </summary>
public interface IClientCredentialsPrincipalProvider
{
    Task<ClaimsPrincipal?> CreatePrincipalAsync(
        string clientId,
        IReadOnlyList<string> scopes,
        CancellationToken cancellationToken = default);
}
