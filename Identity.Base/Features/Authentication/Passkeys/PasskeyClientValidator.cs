using OpenIddict.Abstractions;

namespace Identity.Base.Features.Authentication.Passkeys;

internal sealed class PasskeyClientValidator(IOpenIddictApplicationManager applicationManager)
{
    public async Task<bool> IsValidPublicAuthorizationClientAsync(
        string clientId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(clientId))
        {
            return false;
        }

        var application = await applicationManager.FindByClientIdAsync(clientId, cancellationToken);
        if (application is null ||
            !string.Equals(
                await applicationManager.GetClientTypeAsync(application, cancellationToken),
                OpenIddictConstants.ClientTypes.Public,
                StringComparison.Ordinal))
        {
            return false;
        }

        var permissions = await applicationManager.GetPermissionsAsync(application, cancellationToken);
        return permissions.Contains(OpenIddictConstants.Permissions.Endpoints.Authorization, StringComparer.Ordinal) &&
               permissions.Contains(OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode, StringComparer.Ordinal);
    }
}
