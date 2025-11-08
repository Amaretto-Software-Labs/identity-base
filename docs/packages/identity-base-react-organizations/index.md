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
      <OrganizationsProvider apiBase={identityConfig.authority}>
        <App />
      </OrganizationsProvider>
    </IdentityProvider>
  )
}
```

`OrganizationsProvider` fetches memberships after the user signs in, caches them, and persists the active organization id to `localStorage` so refreshes retain context. Pass `onOrganizationChanged` to react to context changes or supply a custom `fetch`/`storage` implementation for advanced scenarios.

## Public API

- `useOrganizations()` – returns `{ memberships, activeOrganizationId, isLoadingMemberships, membershipError, organizations, isLoadingOrganizations, reloadMemberships, setActiveOrganizationId, client }`.
- `useOrganizationSwitcher()` – updates the active organization id (persists to storage) and refreshes memberships/tokens when needed.
- `useOrganizationMembers(organizationId, query)` – paginated member listing with helpers (`members`, `isLoading`, `ensurePage`, `updateMember`, `removeMember`, `refresh`). Supports server-side filters (`search`, `roleId`, `page`, `pageSize`, `sort`).
- `client` (from `useOrganizations().client`) exposes typed helpers: `listMembers`, `inviteMember`, `revokeInvitation`, `listInvitations`, `getRolePermissions`, `updateRolePermissions`, `listRoles`, `createRole`, `deleteRole`, etc. The list helpers accept `query` objects (e.g., `{ page, pageSize, search, sort }`) so your UI can request the exact slice it needs while still receiving normalized arrays.
- Exported types: `OrganizationMembership`, `OrganizationSummary`, `OrganizationRole`, `OrganizationInvitation`, plus query/paging types like `OrganizationMemberQuery`, `OrganizationRoleListQuery`, and `OrganizationInvitationListQuery`.

## Server Expectations

- Identity Base organizations endpoints must be available: `/users/me/organizations`, `/admin/organizations/{id}/members`, `/admin/organizations/{id}/invitations`, `/admin/organizations/{id}/roles`, `/admin/organizations/{id}/roles/{roleId}/permissions`, etc.
- The SPA must send `X-Organization-Id` with API requests that require an active organization. The provider exposes the current id for convenience.
- There is no API to “set” the active organization; persisting and forwarding the selected id is entirely client-side.
- Send `X-Organization-Id` on API calls that should operate within an organization scope. Changes to membership/role assignments generally require a token refresh (call `IdentityAuthManager.refreshTokens()`) so downstream services pick up the new `org:*` claims.

## Extension Points

- Custom networking: pass a `fetch` prop to integrate with libraries such as React Query or to inject auth headers.
- Persistence: provide a `storage` adapter if you prefer session storage or encrypted storage instead of the default `localStorage`.
- Events: use `onOrganizationChanged` to update UI/global state when the active organization changes.
- Invitations: build bespoke invitation/resend flows by calling `client.inviteMember`/`client.revokeInvitation` and using your own email templates.

## Dependencies & Compatibility

- Requires `@identity-base/react-client`.
- Designed for React 19.
- Aligns with Identity Base organizations (server v0.4.0+ for invitation endpoints).

## Troubleshooting & Tips
- **Header not sent** – ensure you consume `useOrganizations()` or `useOrganizationSwitcher()` before issuing API calls; these hooks provide the selected organization id. Forward it as `X-Organization-Id` on custom fetch calls.
- **Stale memberships** – call `useOrganizations().reloadMemberships()` (or re-invoke `setActiveOrganizationId`) after the backend mutates memberships outside of the current UI flow.
- **Token refresh loop** – when switching organizations or changing memberships, refresh tokens if your UI depends on `org:*` claims (e.g., call `IdentityAuthManager.refreshTokens()`).
- **Optimistic updates** – hooks expose `updateMember`/`removeMember` for optimistic UI updates. Catch thrown `IdentityError`s to revert state when the API rejects a change.

## Examples & Guides

- [Organization Onboarding Flow](../../guides/organization-onboarding-flow.md)
- [Organization Admin Use Case](../../guides/organization-admin-use-case.md)
- Sample SPA: `apps/org-sample-client`

## Change Log

- See [CHANGELOG.md](../../CHANGELOG.md) (`@identity-base/react-organizations` entries)
