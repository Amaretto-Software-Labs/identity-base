# @identity-base/angular-organizations

Angular organizations client for Identity Base. Depends on `@identity-base/angular-client` and `@identity-base/client-core`.

## Install

```bash
npm install @identity-base/angular-organizations @identity-base/angular-client @identity-base/client-core
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

