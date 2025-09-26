using System;
using System.IO;
using Microsoft.Extensions.Options;

namespace Identity.Base.Options;

public sealed class OpenIddictServerKeyOptionsValidator : IValidateOptions<OpenIddictServerKeyOptions>
{
    public ValidateOptionsResult Validate(string? name, OpenIddictServerKeyOptions options)
    {
        if (options is null)
        {
            return ValidateOptionsResult.Fail("OpenIddict server key options are not configured.");
        }

        var provider = options.Provider ?? OpenIddictServerKeyOptions.ProviderDevelopment;

        return provider switch
        {
            OpenIddictServerKeyOptions.ProviderDevelopment => ValidateOptionsResult.Success,
            OpenIddictServerKeyOptions.ProviderFileSystem => ValidateFileOptions(options.File),
            OpenIddictServerKeyOptions.ProviderAzureKeyVault => ValidateAzureOptions(options.AzureKeyVault),
            _ => ValidateOptionsResult.Fail($"Unsupported OpenIddict server key provider: '{provider}'.")
        };
    }

    private static ValidateOptionsResult ValidateFileOptions(FileCertificateOptions options)
    {
        if (options is null)
        {
            return ValidateOptionsResult.Fail("File provider options must be configured.");
        }

        if (string.IsNullOrWhiteSpace(options.Signing?.Path))
        {
            return ValidateOptionsResult.Fail("File provider requires a signing certificate path.");
        }

        if (!File.Exists(options.Signing.Path))
        {
            return ValidateOptionsResult.Fail($"Signing certificate file not found: {options.Signing.Path}.");
        }

        if (options.Encryption is not null && !string.IsNullOrWhiteSpace(options.Encryption.Path) && !File.Exists(options.Encryption.Path))
        {
            return ValidateOptionsResult.Fail($"Encryption certificate file not found: {options.Encryption.Path}.");
        }

        return ValidateOptionsResult.Success;
    }

    private static ValidateOptionsResult ValidateAzureOptions(AzureKeyVaultCertificateOptions options)
    {
        if (options is null)
        {
            return ValidateOptionsResult.Fail("Azure Key Vault options must be configured.");
        }

        if (string.IsNullOrWhiteSpace(options.VaultUri))
        {
            return ValidateOptionsResult.Fail("VaultUri must be provided when using the Azure Key Vault provider.");
        }

        if (!Uri.TryCreate(options.VaultUri, UriKind.Absolute, out _))
        {
            return ValidateOptionsResult.Fail("VaultUri must be a valid absolute URI.");
        }

        if (string.IsNullOrWhiteSpace(options.SigningSecretName))
        {
            return ValidateOptionsResult.Fail("SigningSecretName must be provided when using the Azure Key Vault provider.");
        }

        return ValidateOptionsResult.Success;
    }
}
