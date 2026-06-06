# Server Certificates for OpenIddict (Key Vault or File)

When you run Identity Base outside of Development, you need real signing and encryption certificates so tokens remain valid across restarts and across instances. Identity Base loads these keys from configuration and wires them into OpenIddict automatically, so you can switch providers without code changes.

A signing certificate is always required because it establishes trust in the tokens you issue. An encryption certificate is optional and only needed if you issue encrypted tokens, but many teams keep one available so they can turn on encryption later without a redeploy.

## Objective

Keep token issuance stable and production-safe by using persisted certificates instead of ephemeral development keys. This protects you from tokens breaking after a restart and ensures multi-instance deployments validate the same signatures.

## Usage

Pick a provider (`File` or `AzureKeyVault`) and configure `OpenIddict:ServerKeys` in your host configuration. Identity Base calls `UseConfiguredServerKeys` during startup, so the only thing you have to maintain is configuration and secret storage.

If you are using files, store PFX files on disk and ensure the host process can read them. If you are using Key Vault, upload base64-encoded PFX content as secrets and give the host identity access. In both cases, use the same signing certificate across all instances of the Identity host.

## Example: File-based certificates

```json
{
  "OpenIddict": {
    "ServerKeys": {
      "Provider": "File",
      "File": {
        "Signing": {
          "Path": "/etc/identity/signing.pfx",
          "Password": "change-me"
        },
        "Encryption": {
          "Path": "/etc/identity/encryption.pfx",
          "Password": "change-me"
        }
      }
    }
  }
}
```

Signing is required. Encryption is optional, but recommended if you plan to enable encrypted tokens.

## Example: Azure Key Vault

```json
{
  "OpenIddict": {
    "ServerKeys": {
      "Provider": "AzureKeyVault",
      "AzureKeyVault": {
        "VaultUri": "https://contoso.vault.azure.net/",
        "SigningSecretName": "identity-signing-pfx",
        "SigningSecretPassword": "change-me",
        "EncryptionSecretName": "identity-encryption-pfx",
        "EncryptionSecretPassword": "change-me",
        "ManagedIdentityClientId": "00000000-0000-0000-0000-000000000000"
      }
    }
  }
}
```

Secrets must contain base64-encoded PFX payloads. `EncryptionSecretName` is optional.

## Operational notes

`Provider=Development` is allowed only when `ASPNETCORE_ENVIRONMENT=Development`. Plan certificate rotation so the old signing key remains available while existing tokens expire, otherwise previously issued tokens will be rejected. If you need zero-downtime rotation, overlap keys and re-issue tokens during the overlap window.
