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

`OrganizationsProvider` fetches memberships after the user signs in, caches them, and persists the active organization id to `localStorage` so refreshes retain context.

## Public API

- `useOrganizations()` – returns `{ memberships, activeOrganizationId, activeOrganization, isLoading, error, setActiveOrganizationId, client }`.
- `useOrganizationSwitcher()` – wraps the active-organization POST endpoint and refreshes tokens when the API indicates `requiresTokenRefresh`.
- `useOrganizationMembers(organizationId, query)` – paginated member listing with helpers (`members`, `isLoading`, `ensurePage`, `updateMember`, `removeMember`).
- `client` (from `useOrganizations().client`) exposes typed helpers: `listMembers`, `inviteMember`, `getRolePermissions`, `updateRolePermissions`, `revokeInvitation`, etc.
- Exported types: `OrganizationMembership`, `OrganizationSummary`, `OrganizationRole`, `OrganizationInvitation`, plus query/paging types.

## Server Expectations

- Identity Base organizations endpoints must be available: `/users/me/organizations`, `/users/me/organizations/active`, `/organizations/{id}/members`, `/organizations/{id}/invitations`, `/organizations/{id}/roles`, etc.
- The SPA must send `X-Organization-Id` with API requests that require an active organization. The provider exposes the current id for convenience.
- When `switchOrganization` indicates `requiresTokenRefresh`, call `IdentityAuthManager.refreshTokens()` to pull the new `org:*` claims.

## Extension Points

- Provide a custom fetch implementation via the provider’s `fetch` prop (useful for attaching interceptors or leveraging React Query).
- Override default cache persistence by passing a custom `storage` adapter.
- Listen to organization change events with the `onOrganizationChanged` callback.
- Combine with your own invitation UI by calling the client helpers directly.

## Dependencies & Compatibility

- Requires `@identity-base/react-client`.
- Designed for React 19.
- Aligns with Identity Base organizations (server v0.4.0+ for invitation endpoints).

## Examples & Guides

- [Organization Onboarding Flow](../../guides/organization-onboarding-flow.md)
- [Organization Admin Use Case](../../guides/organization-admin-use-case.md)
- Sample SPA: `apps/org-sample-client`

## Change Log

- See [CHANGELOG.md](../../CHANGELOG.md) (`@identity-base/react-organizations` entries)
