# OpenIddict Key Management Findings

This note captures the issues that surfaced while introducing configurable key management for OpenIddict and the mitigations applied.

## Findings

1. **Ephemeral server keys in production path**  
   `Identity.Base/Extensions/ServiceCollectionExtensions.cs` configured OpenIddict with `AddEphemeralEncryptionKey()` and `AddDevelopmentSigningCertificate()`, causing tokens to be re-signed on every restart and preventing stable validation outside the issuing instance.

2. **Lack of configurable certificate source**  
   With only the dev helpers wired, there was no path to load persisted certificates from disk or Azure Key Vault without changing code.

3. **Tests depending on HTTP base URLs**  
   Integration tests created HTTP clients without a base address; after enforcing HTTPS in the pipeline, the OpenIddict transport security check rejected those requests with 400 responses.

## Mitigations

- Added a provider registry so `UseConfiguredServerKeys` loads signing/encryption material from configuration, with first-class support for `Development`, `File`, and `AzureKeyVault` providers (`Identity.Base/OpenIddict/KeyManagement`).
- Introduced configuration/validation types (`OpenIddictServerKeyOptions*`) and Azure Key Vault + file-system loaders using `X509CertificateLoader` rather than constructing X509 certificates directly.
- Extended the test factory to default to `https://localhost` and disable the transport security requirement inside the test host so existing tests remain stable (`Identity.Base.Tests/HealthzEndpointTests.cs`).

## Follow-up

- Populate `OpenIddict:ServerKeys` with real certificate metadata per environment (file paths, Key Vault secret names, etc.).
- If additional secret stores are needed, register new providers through `OpenIddictServerBuilderExtensions.RegisterServerKeyProvider` without further code churn.
