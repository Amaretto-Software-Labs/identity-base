# React Organisations Add-On

The `@identity-base/react-organisations` package contains reusable helpers for working with
Identity.Base organisation features in React applications. It builds on top of the core
`@identity-base/react-client` library and provides ready-made hooks that encapsulate the common
membership and active-organisation flows.

## Package Contents

- `OrganisationsProvider`  
  Wraps your app and wires the organisation membership context. Accepts an `apiBase` pointing at
  the Identity.Base host (defaults to `window.location.origin` in the browser) and persists the
  currently active organisation to `localStorage`.

- `useOrganisations()`  
  Returns memberships, cached organisation summaries, loading/error state, and a method to switch
  the in-memory active organisation.

- `useOrganisationSwitcher()`  
  Exposes a `switchOrganisation` function that calls `/users/me/organisations/active`, refreshes
  tokens via `IdentityAuthManager.refreshTokens()` when possible, and surfaces whether the flow
  requires an interactive authorization round-trip.

- `useOrganisationMembers()`  
  Fetches paginated `/organisations/{id}/members` results and exposes helpers for filtering,
  sorting, virtualization, and member management. The hook returns the current query state,
  total count, page metadata, `ensurePage()` for prefetching, `getMemberAt()` for virtualized
  lists, and `updateMember()` / `removeMember()` facades over the Identity.Base endpoints.

- `useOrganisations().client`  
  Provides a typed client wrapper around the underlying API. In addition to membership helpers it
  now exposes `getRolePermissions(organisationId, roleId)` and
  `updateRolePermissions(organisationId, roleId, permissions)` so callers can retrieve the effective
  permission set for an organisation role and manage the organisation-specific overrides.

- Exported types such as `Membership`, `OrganisationSummary`, `OrganisationRole`, and
  `OrganisationMember` for convenience in strongly typed apps, plus paging helpers like
  `OrganisationMemberQuery`, `OrganisationMemberQueryState`, and `OrganisationMembersPage`.

## Usage

```tsx
import { IdentityProvider } from '@identity-base/react-client'
import { OrganisationsProvider, useOrganisations } from '@identity-base/react-organisations'

function App() {
  return (
    <IdentityProvider config={identityConfig}>
      <OrganisationsProvider apiBase={identityConfig.apiBase}>
        <YourRoutes />
      </OrganisationsProvider>
    </IdentityProvider>
  )
}

function Dashboard() {
  const { memberships, activeOrganisationId } = useOrganisations()
  // ...
}
```

The org sample client consumes this package and no longer ships bespoke membership hooks.

### Editing Role Permissions

```tsx
const { client } = useOrganisations()

async function savePermissions(organisationId: string, roleId: string, permissions: string[]) {
  // Fetch the current explicit vs. inherited permissions
  const current = await client.getRolePermissions(organisationId, roleId)

  console.log(current.effective) // union of inherited + explicit permissions
  console.log(current.explicit)  // organisation-specific overrides

  // Persist a new override list for the organisation
  await client.updateRolePermissions(organisationId, roleId, permissions)
}
```

The helper automatically scopes calls to the organisation-aware endpoints added in this iteration.
