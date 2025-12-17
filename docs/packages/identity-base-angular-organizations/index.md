# @identity-base/angular-organizations

## Overview
`@identity-base/angular-organizations` is the official Angular SDK for Identity Base organization features. It depends on `@identity-base/angular-client` for authentication and uses the same `apiBase`.

## Installation

```bash
npm install @identity-base/angular-organizations @identity-base/angular-client @identity-base/client-core
```

## Setup

Register providers once (for example in `app.config.ts`):

```ts
import { provideIdentityOrganizations } from '@identity-base/angular-organizations'

export const appConfig = {
  providers: [
    ...provideIdentityOrganizations({
      apiBase: 'https://identity.example.com',
    }),
  ],
}
```

## Services

- `OrganizationsService` – wraps the `/users/me/organizations/...` and `/admin/organizations/...` API surface.
- `ActiveOrganizationService` – holds the active organization id and is used to attach the `X-Organization-Id` header.

## HTTP Interceptor

`OrganizationContextInterceptor` attaches `X-Organization-Id` to Angular `HttpClient` requests (when an active organization is set).

## Change Log
- See `CHANGELOG.md`.

