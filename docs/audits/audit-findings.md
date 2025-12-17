# Identity Frontend/Backend Audit Findings

The following items summarize the issues flagged during the code audit. Line references use 1-based numbering as of the audit snapshot.

## High Severity

1. `Identity.Base/Extensions/ServiceCollectionExtensions.cs:301`
   - **Issue:** Authentication cookies were configured with `SameSite=None` but `CookieSecurePolicy.SameAsRequest`, causing browsers to drop session cookies over HTTPS when the initial request was HTTP and leaving them vulnerable on plaintext channels.
   - **Recommendation:** Force `CookieSecurePolicy.Always` (completed) or gate cookie issuance behind HTTPS-only environments.

2. `Identity.Base/Extensions/ServiceCollectionExtensions.cs:274`
   - **Issue:** OpenIddict server used `AddEphemeralEncryptionKey()` and `AddDevelopmentSigningCertificate()` in all environments, invalidating tokens on restart and reusing dev credentials in production.
   - **Recommendation:** Load persisted signing/encryption certificates via configuration (now addressed via key-provider abstraction).

## Medium Severity

1. `Identity.Base/Features/Authentication/External/ExternalAuthenticationService.cs:340`
   - **Issue:** `BuildCallbackUri` ignored `Request.PathBase` and forwarded headers, generating invalid callback URLs behind reverse proxies or sub-path deployments.
   - **Status:** **Fixed** — now honors forwarded proto/host headers and `PathBase` when constructing callback URLs.

2. `packages/identity-react-client/src/core/TokenManager.ts:98`
   - **Issue:** `ensureValidToken` returned stored access tokens without checking expiration or honoring `autoRefresh`, leading to repeated 401s when tokens expire.
   - **Status:** **Fixed** — JWT expiry is decoded and tokens are refreshed/cleared automatically based on configuration.

3. `packages/identity-react-client/src/react/IdentityProvider.tsx:22`
   - **Issue:** Provider memoized `IdentityAuthManager` via `useState`, so updates to the `config` prop were ignored (e.g., switching tenants).
   - **Status:** **Fixed** — the provider now recreates `IdentityAuthManager` when config changes, keeping state in sync.

## Notes

- High-severity items were remediated earlier by enforcing secure cookies and introducing configurable key management. All medium items above are now resolved in code.
- Add regression tests (unit or integration) to cover externally hosted callbacks, token auto-refresh, and dynamic configuration to prevent regressions.
