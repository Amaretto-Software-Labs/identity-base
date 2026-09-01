# @identity-base/angular-client

Angular DI wrapper for Identity Base, built on `@identity-base/client-core`.

## Install

```bash
npm install @identity-base/angular-client
```

## Setup

```ts
import { provideIdentityClient } from '@identity-base/angular-client'

export const appConfig = {
  providers: [
    ...provideIdentityClient({
      apiBase: 'https://identity.example.com',
      clientId: 'spa-client',
      redirectUri: 'https://app.example.com/auth/callback',
      scope: 'openid profile email identity.api',
      tokenStorage: 'sessionStorage',
      autoRefresh: true,
      loginPath: '/login',
    }),
  ],
}
```

## Route Guard

```ts
import { IdentityRequireAuthGuard } from '@identity-base/angular-client'

export const routes: Routes = [
  {
    path: 'account',
    canActivate: [IdentityRequireAuthGuard],
    loadComponent: () => import('./account.component').then(m => m.AccountComponent),
  },
]
```

The package exports `IDENTITY_AUTH_MANAGER` for direct access to the full client-core surface, including `admin.servicePrincipals`.

## Passkeys

`IdentityPasskeyService` is registered by `provideIdentityClient`:

```ts
const passkeys = inject(IdentityPasskeyService)

if (passkeys.isSupported()) {
  await passkeys.login()
}

await passkeys.beginSignup({
  mode: 'passkey-assisted', // or 'passwordless'
  email,
  metadata: { displayName },
})
```

The service also exposes email confirmation, signup completion, passwordless recovery, and list/create/rename/remove methods. See the [passkey guide](../../docs/guides/passkeys.md).
