# @identity-base/client-core

## Overview

`@identity-base/client-core` is the framework-agnostic TypeScript core used by the React and Angular clients. It implements authorization code + PKCE, access/refresh-token management, cookie fallback for same-origin sessions, account and MFA operations, external-provider helpers, permission lookup, and typed admin APIs for users, roles, permissions, and service principals.

## Installation

```bash
npm install @identity-base/client-core
```

## Public API

### Configuration

```ts
import { IdentityAuthManager } from '@identity-base/client-core'

const authManager = new IdentityAuthManager({
  apiBase: 'https://identity.example.com',
  clientId: 'spa-client',
  redirectUri: 'https://app.example.com/auth/callback',
  scope: 'openid profile email offline_access identity.api identity.admin',
  tokenStorage: 'sessionStorage',
  autoRefresh: true,
  timeout: 10_000,
})
```

`tokenStorage` defaults to `sessionStorage`, `autoRefresh` defaults to `true`, the authorization scope defaults to `openid profile email offline_access identity.api`, and the request timeout defaults to 10 seconds. When auto-refresh is enabled, concurrent refresh requests share one promise and tokens refresh 30 seconds before expiry. The `retries` config field is reserved; the current `ApiClient` does not retry automatically.

### `IdentityAuthManager`

| Area | Methods |
| --- | --- |
| Session and PKCE | `login`, `logout`, `startAuthorization`, `handleAuthorizationCallback`, `refreshTokens`, `getCurrentUser`, `getAccessToken`, `isAuthenticated` |
| Registration and recovery | `register`, `requestPasswordReset`, `resetPassword`, `getProfileSchema` |
| MFA | `sendMfaChallenge`, `verifyMfa`, `enrollMfa`, `disableMfa`, `regenerateRecoveryCodes` |
| Profile and authorization | `updateProfile`, `getUserPermissions` |
| External providers | `buildExternalStartUrl`, `unlinkExternalProvider` |
| Events | `addEventListener` for login, logout, refresh, and error events |

All HTTP requests include `credentials: 'include'`, allowing same-origin Identity cookies to work. Authorized methods attach a bearer token when available and otherwise retain cookie-only behavior. Successful mutation responses may be `204 No Content`; the client returns `undefined` rather than attempting JSON parsing.

Non-success responses accept either Problem Details JSON or plain text. Both become `IdentityError` instances with the HTTP status and useful message/detail preserved.

### Admin Namespaces

Administrative operations are intentionally grouped under `authManager.admin`:

- `admin.users` – list/get/create/update, lock/unlock, password reset, MFA reset, confirmation resend, roles, soft-delete, and restore.
- `admin.roles` – list/create/update/delete.
- `admin.permissions` – paged permission catalog.
- `admin.servicePrincipals` – list/get/create/update, disable/restore, role assignments, and credential issue/revocation.

```ts
const page = await authManager.admin.servicePrincipals.list({
  page: 1,
  pageSize: 25,
  search: 'worker',
  disabled: false,
})

const issued = await authManager.admin.servicePrincipals.issueCredential(
  page.items[0].id,
  { name: 'production', expiresAt: '2027-01-01T00:00:00Z' },
)
```

The service-principal types include list/detail summaries, concurrency-aware update requests, role responses, credential metadata, and the one-time issued-secret response.

### Lower-Level Exports

- `TokenManager` – token storage, authorization-code exchange, refresh de-duplication, expiry checks, and auto-refresh.
- `ApiClient` – timeouts, credentials, response parsing, authorization URL construction, and normalized errors.
- Utilities – `generatePkce`, `PKCEManager`, `createTokenStorage`, `IdentityError`, `createError`, and `enableDebugLogging`.

## Notes

- `startAuthorization()` performs a browser redirect; use it only in a browser environment.
- `getCurrentUser()` uses bearer authentication when a token exists and cookie authentication otherwise; only a 401 is normalized to an anonymous `null` result.
- Request `offline_access` and grant the refresh-token permission to the SPA client if you expect automatic refresh.
- Service-principal secrets are returned once by the server. Persist `issued.secret` immediately in an approved secret store.
- Framework integrations handle routing and component lifecycle, but re-export the same manager and types.

## Related Documentation

- [React Client](../identity-base-react-client/index.md)
- [Angular Client](../identity-base-angular-client/index.md)
- [Service Principals](../identity-base-service-principals/index.md)
- [Full Stack Integration Guide](../../guides/full-stack-integration-guide.md)

## Change Log
- See `CHANGELOG.md`.
