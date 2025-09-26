using System;
using System.Security.Cryptography.X509Certificates;
using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Identity.Base.Options;

namespace Identity.Base.OpenIddict.KeyManagement;

internal sealed class AzureKeyVaultOpenIddictServerKeyProvider : IOpenIddictServerKeyProvider
{
    private readonly AzureKeyVaultCertificateOptions _options;
    private readonly SecretClient _secretClient;

    public AzureKeyVaultOpenIddictServerKeyProvider(AzureKeyVaultCertificateOptions options)
    {
        _options = options;

        var credentialOptions = new DefaultAzureCredentialOptions();
        if (!string.IsNullOrWhiteSpace(options.ManagedIdentityClientId))
        {
            credentialOptions.ManagedIdentityClientId = options.ManagedIdentityClientId;
        }

        TokenCredential credential = new DefaultAzureCredential(credentialOptions);
        _secretClient = new SecretClient(new Uri(options.VaultUri), credential);
    }

    public X509Certificate2? GetSigningCertificate()
    {
        if (string.IsNullOrWhiteSpace(_options.SigningSecretName))
        {
            throw new InvalidOperationException("SigningSecretName must be configured for the Azure Key Vault provider.");
        }

        return LoadCertificate(_options.SigningSecretName, _options.SigningSecretVersion, _options.SigningSecretPassword);
    }

    public X509Certificate2? GetEncryptionCertificate()
    {
        if (string.IsNullOrWhiteSpace(_options.EncryptionSecretName))
        {
            return null;
        }

        return LoadCertificate(_options.EncryptionSecretName, _options.EncryptionSecretVersion, _options.EncryptionSecretPassword);
    }

    private X509Certificate2 LoadCertificate(string secretName, string? secretVersion, string? password)
    {
        try
        {
            KeyVaultSecret secret = secretVersion is null
                ? _secretClient.GetSecret(secretName)
                : _secretClient.GetSecret(secretName, secretVersion);

            if (string.IsNullOrWhiteSpace(secret.Value))
            {
                throw new InvalidOperationException($"Key Vault secret '{secretName}' does not contain a certificate payload.");
            }

            byte[] rawData = Convert.FromBase64String(secret.Value);
            var pwd = password is null ? ReadOnlySpan<char>.Empty : password.AsSpan();
            return X509CertificateLoader.LoadPkcs12(rawData, pwd);
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException($"Key Vault secret '{secretName}' is not a valid base64-encoded certificate.", ex);
        }
        catch (RequestFailedException ex)
        {
            throw new InvalidOperationException($"Failed to retrieve Key Vault secret '{secretName}'.", ex);
        }
    }
}
