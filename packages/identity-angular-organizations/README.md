# @identity-base/angular-organizations

Angular organizations client for Identity Base. Depends on `@identity-base/angular-client` (which depends on `@identity-base/client-core`).

## Install

```bash
npm install @identity-base/angular-organizations
```

## Setup

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
