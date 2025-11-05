# @identity-base/react-client

## Overview
`@identity-base/react-client` is the official React SDK for authenticating against an Identity Base authority. It wraps the authorization-code PKCE flow, manages access/refresh tokens, exposes hooks for account lifecycle (register, login, MFA, profile), and provides an `IdentityAuthManager` for imperative use cases. The library targets React 19 and works with any bundler (Vite, Next.js, CRA).

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

The provider instantiates an `IdentityAuthManager`, performs an initial `getCurrentUser()` call, listens for auth events (login/logout/token-refresh), and exposes the current auth state through React context.

### Configuration (`IdentityConfig`)

| Field | Required | Default | Description |
| --- | --- | --- | --- |
| `apiBase` | ✔ | — | Base URL of the Identity Base authority (e.g., `https://identity.example.com`). |
| `clientId` | ✔ | — | The SPA client id registered under `OpenIddict:Applications`. |
| `redirectUri` | ✔ | — | PKCE redirect URI that matches the client configuration. |
| `scope` | ✖ | `openid profile email identity.api` | Space-delimited scope string requested during login. Include additional API scopes as needed. |
| `tokenStorage` | ✖ | `sessionStorage` | Where tokens are persisted: `'localStorage'`, `'sessionStorage'`, or `'memory'`. |
| `autoRefresh` | ✖ | `true` | Automatically attempt silent refresh before tokens expire. |
| `timeout` | ✖ | `10000` | Fetch timeout (ms) when calling Identity Base APIs. |
| `retries` | ✖ | `0` | Number of retry attempts for transient failures. |

## Public API

### Hooks & Components

| Hook / Component | Purpose |
| --- | --- |
| `useAuth()` | Returns `{ user, isAuthenticated, isLoading, error, authManager }`. Primary hook for auth state. |
| `useLogin()` | Helpers for email/password login and PKCE exchange. |
| `useRegister()` | Wraps `/auth/register`; accepts metadata map matching Identity Base profile schema. |
| `useForgotPassword()` / `useResetPassword()` | Manage password reset flows end-to-end. |
| `useMfa()` | Initiate and verify MFA challenges (authenticator/email/SMS). |
| `useProfile()` | Fetch/update the `/users/me` profile and handle concurrency stamps. |
| `useAuthorization()` / `usePermissions()` | Read effective permission claims (`identity.permissions`). |
| `useAdminUsers`, `useAdminUser`, `useAdminRoles`, `useAdminPermissions` | Convenience hooks for the admin API (built on top of the same client). |
| `useRequireAuth()` | Gate routes/client logic until the user is authenticated. |
| `<ProtectedRoute>` / `<RequireAuth>` | Components for guarding React Router routes or JSX blocks. |

### IdentityAuthManager (imperative API)

```ts
const manager = useAuth().authManager
await manager.loginWithPassword({ email, password, clientId: 'spa-client' })
await manager.refreshTokens()
await manager.logout()
```

The manager also exposes lower-level helpers (`generatePkce`, `completeSignIn`, `fetchWithAuth`) for advanced scenarios.

## Usage Patterns

- **Handling registration metadata** – call `useRegister().register({ email, password, metadata })`. Retrieve the expected fields beforehand via `useProfile().getProfileSchema()` or the `/auth/profile-schema` endpoint.
- **MFA challenge** – `useMfa()` exposes `sendChallenge(method)` and `verifyCode({ method, code })`. Methods returned by Identity Base include `authenticator`, `email`, `sms`, and `recovery`.
- **Token refresh** – when backend responses indicate `requiresTokenRefresh` (e.g., organization invitation acceptance), call `authManager.refreshTokens()`.
- **Error handling** – hooks throw `IdentityError` objects; inspect `error.code` and `error.message` before showing a user-friendly message.

## Dependencies & Compatibility
- Requires React 19.
- Expects modern browsers with Fetch API and Web Crypto support (polyfill if targeting older environments).
- Designed to work alongside `@identity-base/react-organizations` (the organizations provider consumes the same `IdentityProvider`).

## Troubleshooting & Tips
- **Infinite loading** – ensure the provider’s `config` instance is stable; changing the object on every render recreates the manager. Memoise or define config outside the component body.
- **401 after refresh** – verify the SPA requested the `offline_access` scope if you expect refresh tokens, and that `autoRefresh` is enabled.
- **Missing permissions** – use `usePermissions()` to inspect the current claim set; if empty, confirm the API host has `MapIdentityRolesUserEndpoints()` wired and the user has roles assigned.
- **Debug logging** – call `enableDebugLogging()` (or set `window.__enableIdentityDebug = true`) to emit verbose logs during development.

## Examples & Guides
- [React Integration Guide](../../guides/react-integration-guide.md)
- [Organization Onboarding Flow](../../guides/organization-onboarding-flow.md)
- Sample SPA (`apps/org-sample-client`)
- Playbook: ../../playbooks/react-client-pkce-login.md

## Change Log
- See [CHANGELOG.md](../../CHANGELOG.md) (`@identity-base/react-client` entries)
