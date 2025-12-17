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
    }),
  ],
}
```
