# @identity-base/react-client

## Overview
`@identity-base/react-client` is the official React SDK for authenticating against an Identity Base authority. It wraps the authorization-code PKCE flow, manages access/refresh tokens, exposes hooks for account lifecycle (register, login, MFA, profile), and provides an `IdentityAuthManager` for imperative use cases. The library supports React 18 and 19 and works with modern bundlers. It builds on (and depends on) `@identity-base/client-core`.

## Installation & Setup

```bash
pnpm add @identity-base/react-client
# or npm/yarn equivalents
```

Wrap your application with the `IdentityProvider`:

```tsx
import { IdentityProvider } from '@identity-base/react-client'

const identityConfig = {
  apiBase: 'https://identity.example.com',
  clientId: 'spa-client',
  redirectUri: 'https://app.example.com/auth/callback',
  scope: 'openid profile email identity.api',
  tokenStorage: 'localStorage',
  autoRefresh: true
}

export function Root() {
  return (
    <IdentityProvider config={identityConfig}>
      <App />
    </IdentityProvider>
  )
}
```

The provider instantiates an `IdentityAuthManager`, performs an initial `getCurrentUser()` call, listens for auth events, and exposes the current auth state through React context. When meaningful config fields change, it clears stale user state and keeps `isLoading=true` until the replacement manager finishes initializing. Non-401 initialization failures are exposed through `error`.

### Configuration (`IdentityConfig`)

| Field | Required | Default | Description |
| --- | --- | --- | --- |
| `apiBase` | ✔ | — | Base URL of the Identity Base authority (e.g., `https://identity.example.com`). |
| `clientId` | ✔ | — | The SPA client id registered under `OpenIddict:Applications`. |
| `redirectUri` | ✔ | — | PKCE redirect URI that matches the client configuration. |
| `scope` | ✖ | `openid profile email offline_access identity.api` | Space-delimited scope string requested during login. Include additional API scopes as needed. |
| `tokenStorage` | ✖ | `sessionStorage` | Where tokens are persisted: `'localStorage'`, `'sessionStorage'`, or `'memory'`. |
| `autoRefresh` | ✖ | `true` | Automatically attempt silent refresh before tokens expire. |
| `timeout` | ✖ | `10000` | Fetch timeout (ms) when calling Identity Base APIs. |
| `retries` | ✖ | `0` | Reserved configuration field; the current core client does not retry automatically. |

## Public API

### Hooks & Components

| Hook / Component | Purpose |
| --- | --- |
| `useAuth()` | Returns `{ user, isAuthenticated, isLoading, error, refreshUser, logout }`. Primary hook for auth state. |
| `useLogin()` | Helpers for email/password login and PKCE exchange. |
| `useRegister()` | Wraps `/auth/register`; accepts metadata map matching Identity Base profile schema. |
| `useForgotPassword()` / `useResetPassword()` | Manage password reset flows end-to-end. |
| `useMfa()` | Initiate and verify MFA challenges (authenticator/email/SMS). |
| `useProfile()` | Fetch/update the `/users/me` profile and handle concurrency stamps. |
| `useAuthorization()` / `usePermissions()` | Read effective permission claims (`identity.permissions`). |
| `useAdminUsers`, `useAdminUser`, `useAdminRoles`, `useAdminPermissions` | Convenience hooks for the admin API (built on top of the same client). |
| `useRequireAuth()` | Gate routes/client logic until the user is authenticated. |
| `<ProtectedRoute>` / `<RequireAuth>` | Components for guarding React Router routes or JSX blocks. |

Use `useIdentityContext()` when you need direct access to `authManager`.

### IdentityAuthManager (imperative API)

```ts
const { authManager: manager } = useIdentityContext()
await manager.login({ email, password, clientId: 'spa-client' })
await manager.refreshTokens()
await manager.logout()
```

The manager also exposes `startAuthorization`, `handleAuthorizationCallback`, profile/MFA/external-provider methods, and the typed `admin.users`, `admin.roles`, `admin.permissions`, and `admin.servicePrincipals` namespaces. PKCE and storage primitives are exported separately for advanced scenarios.

## Usage Patterns

- **Handling registration metadata** – call `useRegister().register({ email, password, metadata })`. Retrieve the expected fields beforehand via `useProfile().getProfileSchema()` or the `/auth/profile-schema` endpoint.
- **MFA challenge** – `useMfa()` exposes `sendChallenge({ method })` and `verifyChallenge({ method, code })`. Methods returned by Identity Base include `authenticator`, `email`, `sms`, and `recovery`.
- **Token refresh** – when backend responses indicate `requiresTokenRefresh` (e.g., organization invitation acceptance), call `authManager.refreshTokens()`.
- **Error handling** – hooks throw `IdentityError` objects; inspect `error.code` and `error.message` before showing a user-friendly message.
- **Route guards** – `ProtectedRoute` renders its fallback while auth initializes and redirects only after loading completes. Provide `redirectTo` or `onUnauthenticated`; otherwise it uses `/login?returnUrl=...`.

## Dependencies & Compatibility
- Requires React 18 or 19.
- Expects modern browsers with Fetch API and Web Crypto support (polyfill if targeting older environments).
- Designed to work alongside `@identity-base/react-organizations` (the organizations provider consumes the same `IdentityProvider`).

## Troubleshooting & Tips
- **Config changes** – equivalent config objects are tolerated. Changing `apiBase`, `clientId`, `redirectUri`, scope, storage, auto-refresh, timeout, or retry settings intentionally resets auth state and initializes a replacement manager.
- **401 after refresh** – verify the SPA requested the `offline_access` scope if you expect refresh tokens, and that `autoRefresh` is enabled.
- **Missing permissions** – use `usePermissions()` to inspect the current claim set; if empty, confirm the API host has `MapIdentityRolesUserEndpoints()` wired and the user has roles assigned.
- **Debug logging** – call `enableDebugLogging()` (or set `window.__enableIdentityDebug = true`) to emit verbose logs during development.

## Examples & Guides
- [React Integration Guide](../../guides/react-integration-guide.md)
- [Organization Onboarding Flow](../../guides/organization-onboarding-flow.md)
- Sample SPA (`apps/org-sample-client`)
- Playbook: ../../playbooks/react-client-pkce-login.md

## Change Log
- See [CHANGELOG.md](../../../CHANGELOG.md) (`@identity-base/react-client` entries)
