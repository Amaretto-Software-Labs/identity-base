# @identity-base/react-client

## Overview
`@identity-base/react-client` is the official React SDK for interacting with an Identity Base authority. It wraps the authorization-code PKCE flow, handles silent refresh, exposes hooks for authentication state, and provides a handy `IdentityAuthManager` for manual token operations. The package targets React 19 and is designed to coordinate with SPAs built on modern tooling (Vite, Next.js, etc.).

## Installation & Setup

```bash
# with pnpm
pnpm add @identity-base/react-client
# npm / yarn equivalents work as well
```

Wrap your application with the `IdentityProvider` and supply configuration that points at your Identity Base authority:

```tsx
import { IdentityProvider } from '@identity-base/react-client'

const identityConfig = {
  authority: 'https://identity.example.com',
  clientId: 'spa-client',
  redirectUri: 'https://app.example.com/auth/callback',
  postLogoutRedirectUri: 'https://app.example.com',
  scopes: ['openid', 'profile', 'email', 'identity.api'],
}

export function Root() {
  return (
    <IdentityProvider config={identityConfig}>
      <App />
    </IdentityProvider>
  )
}
```

The provider manages PKCE code verifier generation, token storage (session storage by default), and opens a pop-up/redirect when initiating sign-in.

## Configuration

`IdentityProvider` accepts the following configuration fields:

- `authority` – Identity Base host URL (required).
- `clientId` – SPA client id registered with Identity Base (required).
- `redirectUri`, `postLogoutRedirectUri` – URIs whitelisted on the OpenIddict application.
- `scopes` – additional scopes beyond the defaults (`openid`, `profile`, `email`).
- `storage` – optional custom storage implementation (defaults to `sessionStorage` with fallback to `memory`).
- `refreshLeewaySeconds` – how soon before expiration the library attempts a silent refresh.
- Callbacks: `onAuthEvent`, `onTokenRefreshed`, `onSessionExpired`.

Configuration can be loaded from environment-specific JSON and passed through the provider.

## Public API

### Components & Hooks

- `<IdentityProvider config={...}>` – root context provider.
- `useIdentity()` – returns authentication state (`isAuthenticated`, `user`, `loading`, etc.).
- `useAuthManager()` – returns the `IdentityAuthManager` instance for imperative login/logout/refresh calls.
- `useUserProfile()` – convenience hook for accessing the cached `/users/me` profile.
- `IdentityAuthManager` API:
  - `signInRedirect()`, `signInPopup()`, `completeSignIn()` – PKCE login helpers.
  - `signOutRedirect()`, `completeSignOut()` – logout flows.
  - `refreshTokens()` – explicit silent refresh.
  - `fetchWithAuth(input, init)` – fetch wrapper that injects the access token.

### Types

- `IdentityProviderConfig`, `AuthState`, `AuthEvent`, `UserProfile`.

## Extension Points

- Supply a custom storage adapter via the `storage` config (implement `{ get, set, remove }`).
- Override fetch behaviour with `IdentityAuthManager.configure({ fetch: customFetch })`.
- Listen to authentication transitions by providing `onAuthEvent`.
- Integrate with your error boundary by inspecting `authState.error`.

## Dependencies & Compatibility

- Peer dependency on `react@^19`.
- Designed to work with browsers supporting the Fetch API and Web Crypto (polyfill as needed).
- Works in tandem with `@identity-base/react-organizations` for organization-aware UIs.

## Examples & Guides

- [React Integration Guide](../../guides/react-integration-guide.md)
- [Organization Onboarding Flow](../../guides/organization-onboarding-flow.md)
- Sample SPA: `apps/org-sample-client`

## Change Log

- See [CHANGELOG.md](../../CHANGELOG.md) (`@identity-base/react-client` entries)
