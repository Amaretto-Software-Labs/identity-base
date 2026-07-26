# @identity-base/angular-organizations

Angular organizations client for Identity Base. Depends on `@identity-base/angular-client` (which depends on `@identity-base/client-core`).

The `OrganizationsService` provides typed `invitations`, `user`, and `admin` namespaces for organization, member, role, permission, and invitation workflows. It supports paged query contracts, bearer/cookie authentication, active `X-Organization-Id` context, plain-text or Problem Details errors, and empty `204` responses.

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

Full documentation: [docs/packages/identity-base-angular-organizations/index.md](../../docs/packages/identity-base-angular-organizations/index.md).
