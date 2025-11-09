---
title: Migrate from 0.7.1 to 0.7.2
description: Breaking API changes in React clients to adopt explicit user/admin namespaces
---

# 0.7.1 → 0.7.2 Migration Guide

This release introduces explicit route namespaces in the React client libraries and removes generic methods. It is a breaking change intended to make call sites choose the correct route family (user vs admin) and avoid accidental permission issues (e.g., passing `X-Organization-Id` to admin routes).

## Summary of Breaking Changes

- @identity-base/identity-react-organizations
  - Removed generic methods on the client. Use explicit namespaces:
    - User-scoped: `client.user.*` for `/users/me/organizations/...`
    - Admin: `client.admin.*` for `/admin/organizations/...`
- @identity-base/react-client
  - Moved admin APIs under `authManager.admin.*`. All top-level admin methods were removed.
- Sample apps
  - Updated to prefer user-scoped organization endpoints.

## Organizations React Client

Old (0.7.1):

```
const org = await client.getOrganization(orgId)
const page = await client.listMembers(orgId, { page: 1 })
await client.updateMember(orgId, userId, { roleIds })
const roles = await client.listRoles(orgId)
const perms = await client.getRolePermissions(orgId, roleId)
```

New (0.7.2): choose the correct namespace explicitly.

User routes:

```
const org = await client.user.getOrganization(orgId)
const page = await client.user.listMembers(orgId, { page: 1 })
await client.user.updateMember(orgId, userId, { roleIds })
const roles = await client.user.listRoles(orgId)
const perms = await client.user.getRolePermissions(orgId, roleId)
```

Admin routes:

```
const org = await client.admin.getOrganization(orgId)
const page = await client.admin.listMembers(orgId, { page: 1 })
```

Notes:

- The provider still forwards `X-Organization-Id` when `activeOrganizationId` is set. User-scoped endpoints do not require the header; admin endpoints ignore it.
- Hooks like `useOrganizationMembers` already use user-scoped routes internally and require no changes.

## React Identity Client (@identity-base/react-client)

Old (0.7.1): top-level admin methods

```
await authManager.listAdminUsers({ page: 1 })
await authManager.createAdminRole({ name: 'X' })
await authManager.listAdminPermissions()
```

New (0.7.2): admin namespace

```
await authManager.admin.users.list({ page: 1 })
await authManager.admin.roles.create({ name: 'X' })
await authManager.admin.permissions.list()
```

Additional mappings:

- Users
  - `getAdminUser(id)` → `admin.users.get(id)`
  - `updateAdminUser(id, payload)` → `admin.users.update(id, payload)`
  - `lockAdminUser(id, payload?)` → `admin.users.lock(id, payload)`
  - `unlockAdminUser(id)` → `admin.users.unlock(id)`
  - `forceAdminPasswordReset(id)` → `admin.users.forcePasswordReset(id)`
  - `resetAdminUserMfa(id)` → `admin.users.resetMfa(id)`
  - `resendAdminConfirmation(id)` → `admin.users.resendConfirmation(id)`
  - `getAdminUserRoles(id)` → `admin.users.getRoles(id)`
  - `updateAdminUserRoles(id, payload)` → `admin.users.updateRoles(id, payload)`
  - `softDeleteAdminUser(id)` → `admin.users.softDelete(id)`
  - `restoreAdminUser(id)` → `admin.users.restore(id)`
- Roles
  - `listAdminRoles(q?)` → `admin.roles.list(q?)`
  - `createAdminRole(p)` → `admin.roles.create(p)`
  - `updateAdminRole(id, p)` → `admin.roles.update(id, p)`
  - `deleteAdminRole(id)` → `admin.roles.delete(id)`
- Permissions
  - `listAdminPermissions(q?)` → `admin.permissions.list(q?)`

No changes to user flows like `getCurrentUser()`, `updateProfile()`, or `getUserPermissions()`.

## Sample Apps

- `org-sample-client`
  - Config routes updated to `/users/me/organizations/{id}/...` for org details, members, roles, and role permissions.
  - Invitation endpoints remain under the sample API.

## Tips

- Do not rely on fallback between admin and user routes. Call the intended namespace directly.
- If you previously saw 403s for owners hitting admin endpoints with `X-Organization-Id`, switch to the user routes (`client.user.*`).

## Checklist

- Update imports/usages of the Organizations client:
  - Replace top-level calls with `client.user.*` or `client.admin.*`.
- Update React Identity client usages:
  - Replace `listAdmin*`/`getAdmin*`/`updateAdmin*` calls with `authManager.admin.*` equivalents.
- Verify sample or custom SPAs use the user-scoped routes for org flows.

