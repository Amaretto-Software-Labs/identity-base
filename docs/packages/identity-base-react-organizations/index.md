# @identity-base/react-organizations

## Overview
`@identity-base/react-organizations` extends the core React client with organization-aware state management. It synchronises memberships from the Identity Base organizations APIs, tracks the active organization locally, and provides hooks for listing members, managing invitations, and editing organization roles. The package expects the backend to expose the endpoints provided by `Identity.Base.Organizations`.

## Installation & Setup

```bash
pnpm add @identity-base/react-organizations
```

Wrap your application with both providers:

```tsx
import { IdentityProvider } from '@identity-base/react-client'
import { OrganizationsProvider } from '@identity-base/react-organizations'

export function Root() {
  return (
    <IdentityProvider config={identityConfig}>
      <OrganizationsProvider apiBase={identityConfig.apiBase}>
        <App />
      </OrganizationsProvider>
    </IdentityProvider>
  )
}
```

`OrganizationsProvider` fetches all membership pages after the user signs in, caches organization summaries, and persists the active organization id to `localStorage` so refreshes retain context. Use `setActiveOrganizationId` or `switchActiveOrganization` to react to context changes. Pass `fetcher` to supply a custom Fetch-compatible implementation and `storageKey` to change the local-storage key.

## Public API

- `useOrganizations()` – returns memberships, active-organization state, loading/errors, `reloadMemberships`, `setActiveOrganizationId`, `switchActiveOrganization`, and `client`.
- `useOrganizationSwitcher()` – validates that the requested organization belongs to the current membership set, loads its summary if needed, and persists the active id. It does not refresh tokens.
- `useOrganizationMembers(organizationId, { fetchOnMount, initialQuery })` – paginated member listing with `members`, `isLoading`, `ensurePage`, `reload`, `updateMember`, and `removeMember`. The initial query supports `search`, `roleId`, `page`, `pageSize`, and `sort`.
- `client.invitations` exposes public invitation `preview` and authenticated `claim`.
- `client.user` exposes organization create/read/update; member list/add/update/remove; role list/create/delete and permission operations; and invitation list/create/revoke.
- `client.admin` exposes organization list/create/read/update/archive and the corresponding member, role, permission, and invitation operations.
- Exported types include `Membership`, `OrganizationSummary`, `OrganizationMember`, `OrganizationRole`, `OrganizationInvitation`, and their option/query/page types.

## Server Expectations

- Identity Base organizations endpoints must be available: `/users/me/organizations`, `/admin/organizations/{id}/members`, `/admin/organizations/{id}/invitations`, `/admin/organizations/{id}/roles`, `/admin/organizations/{id}/roles/{roleId}/permissions`, etc.
- The SPA must send `X-Organization-Id` with API requests that require an active organization. The built-in client attaches it to scoped admin requests once an active organization is selected; user-membership and public invitation routes remain unscoped.
- There is no API to “set” the active organization; persisting and forwarding the selected id is entirely client-side.
- Send `X-Organization-Id` on API calls that should operate within an organization scope. Changes to membership/role assignments generally require a token refresh (call `IdentityAuthManager.refreshTokens()`) so downstream services pick up the new `org:*` claims.

## Extension Points

- Custom networking: pass a `fetcher` prop with the standard Fetch signature.
- Persistence: pass `storageKey` to choose the `localStorage` key used for the active organization.
- Invitations: build invitation flows with `client.user.createInvitation`, `client.admin.createInvitation`, `client.invitations.preview`, and `client.invitations.claim`.

## Dependencies & Compatibility

- Requires `@identity-base/react-client`.
- Designed for React 19.
- Aligns with Identity Base organizations (server v0.4.0+ for invitation endpoints).

## Troubleshooting & Tips
- **Header not sent** – ensure you consume `useOrganizations()` or `useOrganizationSwitcher()` before issuing API calls; these hooks provide the selected organization id. Forward it as `X-Organization-Id` on custom fetch calls.
- **Stale memberships** – call `useOrganizations().reloadMemberships()` after the backend mutates memberships outside of the current UI flow.
- **Token refresh loop** – when switching organizations or changing memberships, refresh tokens if your UI depends on `org:*` claims (e.g., call `IdentityAuthManager.refreshTokens()`).
- **Optimistic updates** – hooks expose `updateMember`/`removeMember` for optimistic UI updates. Catch thrown `IdentityError`s to revert state when the API rejects a change.

## Examples & Guides

- [Organization Onboarding Flow](../../guides/organization-onboarding-flow.md)
- [Organization Admin Use Case](../../guides/organization-admin-use-case.md)
- Sample SPA: `apps/org-sample-client`

## Change Log

- See [CHANGELOG.md](../../CHANGELOG.md) (`@identity-base/react-organizations` entries)
