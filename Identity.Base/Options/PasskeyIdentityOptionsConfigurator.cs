using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Identity.Base.Options;

internal sealed class PasskeyIdentityStoreOptionsConfigurator(
    IOptions<PasskeyOptions> options) : IConfigureOptions<IdentityOptions>
{
    public void Configure(IdentityOptions identityOptions)
    {
        if (options.Value.Enabled)
        {
            identityOptions.Stores.SchemaVersion = IdentitySchemaVersions.Version3;
        }
    }
}

internal sealed class PasskeyIdentityOptionsConfigurator(
    IOptions<PasskeyOptions> options) : IConfigureOptions<IdentityPasskeyOptions>
{
    public void Configure(IdentityPasskeyOptions identityOptions)
    {
        var passkeys = options.Value;
        if (!passkeys.Enabled)
        {
            return;
        }

        identityOptions.ServerDomain = passkeys.ServerDomain;
        identityOptions.AuthenticatorTimeout = TimeSpan.FromSeconds(passkeys.AuthenticatorTimeoutSeconds);
        identityOptions.ChallengeSize = passkeys.ChallengeSize;
        identityOptions.UserVerificationRequirement = passkeys.UserVerification;
        identityOptions.ResidentKeyRequirement = passkeys.ResidentKey;
        identityOptions.AttestationConveyancePreference = passkeys.Attestation;
        identityOptions.ValidateOrigin = context =>
        {
            var normalized = PasskeyOptionsValidator.NormalizeOrigin(context.Origin);
            return ValueTask.FromResult(
                passkeys.AllowedOrigins.Any(
                    allowed => string.Equals(
                        PasskeyOptionsValidator.NormalizeOrigin(allowed),
                        normalized,
                        StringComparison.Ordinal)));
        };
    }
}
