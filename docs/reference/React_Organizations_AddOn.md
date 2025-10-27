# React Organizations Add-On

The `@identity-base/react-organizations` package contains reusable helpers for working with
Identity.Base organization features in React applications. It builds on top of the core
`@identity-base/react-client` library and provides ready-made hooks that encapsulate the common
membership and active-organization flows.

## Package Contents

- `OrganizationsProvider`  
  Wraps your app and wires the organization membership context. Accepts an `apiBase` pointing at
  the Identity.Base host (defaults to `window.location.origin` in the browser) and persists the
  currently active organization to `localStorage`.

- `useOrganizations()`  
  Returns memberships, cached organization summaries, loading/error state, and a method to switch
  the in-memory active organization.

- `useOrganizationSwitcher()`  
  Exposes a `switchOrganization` function that calls `/users/me/organizations/active`, refreshes
  tokens via `IdentityAuthManager.refreshTokens()` when possible, and surfaces whether the flow
  requires an interactive authorization round-trip.

- `useOrganizationMembers()`  
  Fetches paginated `/organizations/{id}/members` results and exposes helpers for filtering,
  sorting, virtualization, and member management. The hook returns the current query state,
  total count, page metadata, `ensurePage()` for prefetching, `getMemberAt()` for virtualized
  lists, and `updateMember()` / `removeMember()` facades over the Identity.Base endpoints.

- Exported types such as `Membership`, `OrganizationSummary`, `OrganizationRole`, and
  `OrganizationMember` for convenience in strongly typed apps, plus paging helpers like
  `OrganizationMemberQuery`, `OrganizationMemberQueryState`, and `OrganizationMembersPage`.

## Usage

```tsx
import { IdentityProvider } from '@identity-base/react-client'
import { OrganizationsProvider, useOrganizations } from '@identity-base/react-organizations'

function App() {
  return (
    <IdentityProvider config={identityConfig}>
      <OrganizationsProvider apiBase={identityConfig.apiBase}>
        <YourRoutes />
      </OrganizationsProvider>
    </IdentityProvider>
  )
}

function Dashboard() {
  const { memberships, activeOrganizationId } = useOrganizations()
  // ...
}
```

The org sample client consumes this package and no longer ships bespoke membership hooks.
