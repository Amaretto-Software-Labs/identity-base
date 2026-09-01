# @identity-base/client-core

Framework-agnostic TypeScript client core for Identity Base. It supports PKCE, token refresh, same-origin cookie fallback, registration/recovery/MFA/profile flows, external providers, permissions, and typed admin APIs for users, roles, permissions, and service principals.

## Install

```bash
npm install @identity-base/client-core
```

## Usage

```ts
import { IdentityAuthManager } from '@identity-base/client-core'

const auth = new IdentityAuthManager({
  apiBase: 'https://identity.example.com',
  clientId: 'spa-client',
  redirectUri: 'https://app.example.com/auth/callback',
  scope: 'openid profile email identity.api',
  tokenStorage: 'sessionStorage',
  autoRefresh: true,
})

await auth.startAuthorization()

// Administrative APIs use explicit namespaces.
const principals = await auth.admin.servicePrincipals.list({ page: 1, pageSize: 25 })
```

Passkeys are orchestrated end-to-end in the browser:

```ts
if (auth.isPasskeySupported()) {
  await auth.loginWithPasskey()
}

await auth.beginPasskeySignup({
  mode: 'passwordless', // or 'passkey-assisted'
  email: 'alice@example.com',
  metadata: { displayName: 'Alice' },
})
```

The same manager exposes email confirmation, signup completion, passwordless recovery, and `listPasskeys`/`createPasskey`/`renamePasskey`/`removePasskey`. See the [passkey guide](../../docs/guides/passkeys.md).

`tokenStorage` defaults to `sessionStorage` and `autoRefresh` defaults to `true`. Requests include credentials, attach bearer tokens when available, accept Problem Details or plain-text errors, and handle empty `204` responses.

Full documentation: [docs/packages/identity-base-client-core/index.md](../../docs/packages/identity-base-client-core/index.md).
