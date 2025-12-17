# @identity-base/angular-client

## Overview
`@identity-base/angular-client` is the official Angular SDK layer for Identity Base. It wraps `@identity-base/client-core` with Angular DI services and an optional `HttpInterceptor` for attaching Bearer tokens.

## Installation

```bash
npm install @identity-base/angular-client @identity-base/client-core
```

## Setup

Register providers once (for example in `app.config.ts`):

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

## Services

- `IdentityAuthService` – wraps the core `IdentityAuthManager`, exposes `user$`, and provides `startAuthorization()` / `handleAuthorizationCallback()`.
- `IdentityAdminService` – exposes `users`, `roles`, and `permissions` admin namespaces.

## HTTP Interceptor

`IdentityAuthInterceptor` attaches `Authorization: Bearer <token>` when the request URL matches `apiBase` (or configured `tokenAttachment` rules).

## Change Log
- See `CHANGELOG.md`.

