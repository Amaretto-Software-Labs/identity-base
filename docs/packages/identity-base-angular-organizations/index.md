# @identity-base/angular-organizations

## Overview
`@identity-base/angular-organizations` is the official Angular SDK for Identity Base organization features. It depends on `@identity-base/angular-client` for authentication and uses the same `apiBase`.

## Installation

```bash
npm install @identity-base/angular-organizations
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

- `OrganizationsService` – exposes explicit `invitations`, `user`, and `admin` namespaces. User/admin namespaces each group `organizations`, `members`, `roles`, and `invitations` operations with typed paging/filter contracts.
- `ActiveOrganizationService` – holds the active organization id and is used to attach the `X-Organization-Id` header.

```ts
const page = await organizations.user.organizations.list({ page: 1, pageSize: 25 })
const roles = await organizations.user.roles.list(page.items[0].organizationId)

const adminPage = await organizations.admin.organizations.list({ search: 'acme' })
const preview = await organizations.invitations.preview(invitationCode)
```

Requests include cookies, attach a bearer token when available, accept empty `204` responses, and normalize both Problem Details and plain-text failures into the shared client-core error type.

## HTTP Interceptor

`OrganizationContextInterceptor` attaches `X-Organization-Id` to Angular `HttpClient` requests when an active organization is set. Configure `organizationHeader.headerName`, `include`, and `exclude` rules when only selected URL prefixes should carry organization context. The package’s own fetch-based service uses the same rules and defaults to the configured `apiBase`.

## Change Log
- See `CHANGELOG.md`.
