# Role-Based Access Control (RBAC) Design

This document captures the RBAC model that underpins the upcoming `Identity.Base.Roles` and `Identity.Base.Admin` packages. It serves as the source of truth for entities, configuration shape, and audit requirements.

---

## Domain Concepts

| Concept | Description |
| --- | --- |
| **Permission** | Atomic capability identifier (e.g., `users.read`, `roles.manage`). Permissions are referenced by roles. |
| **Role** | Named collection of permissions. Users may hold multiple roles. |
| **UserRole** | Association linking a user to a role. Effective permissions are the union of permissions from all assigned roles. |
| **Audit Entry** | Record of an administrative action (actor, target user, action name, serialized metadata, timestamp). |

### Entity summary
- `Role`
  - `Id` (GUID)
  - `Name` (string, unique)
  - `Description` (nullable string)
  - `IsSystemRole` (bool, protects seed roles from deletion)
  - `ConcurrencyStamp` (string for optimistic concurrency)
- `Permission`
  - `Id` (GUID)
  - `Name` (string, unique)
  - `Description` (nullable string)
- `RolePermission`
  - `RoleId`, `PermissionId` (composite PK)
- `UserRole`
  - `UserId`, `RoleId` (composite PK)
- `AuditEntry`
  - `Id` (GUID)
  - `ActorUserId` (GUID)
  - `TargetUserId` (GUID?)
  - `Action` (string, e.g., `users.lock`)
  - `Metadata` (JSONB)
  - `CreatedAt` (timestamp)

### Initial permission catalogue
| Permission | Notes |
| --- | --- |
| `users.read` | List/view other users & details (admin only) |
| `users.create` | Create users |
| `users.update` | Edit user profile/flags |
| `users.lock` | Lock/unlock accounts |
| `users.reset-password` | Force password reset email |
| `users.reset-mfa` | Reset MFA enrollment |
| `users.delete` | Soft delete & restore users |
| `users.manage-roles` | Assign/remove roles for users |
| `roles.read` | View role catalog |
| `roles.manage` | Create/update/delete roles & permissions |

Add new permissions as features evolve. Permissions should be expressed as lowercase kebab-case strings to remain consistent. The self-service endpoints (`/users/me`, `/users/me/profile`) remain accessible without explicit permissions so StandardUser can operate with an empty permission list.

---

## Configuration Schema

### Role definitions
```jsonc
"Roles": {
  "Definitions": [
    {
      "name": "StandardUser",
      "description": "Base access for self-service users",
      "permissions": []
    },
    {
      "name": "SupportAgent",
      "description": "Support staff with limited admin powers",
      "permissions": [
        "users.read",
        "users.lock",
        "users.reset-password",
        "users.reset-mfa"
      ]
    },
    {
      "name": "IdentityAdmin",
      "description": "Full administrative access",
      "permissions": [
        "users.read",
        "users.create",
        "users.update",
        "users.lock",
        "users.reset-password",
        "users.reset-mfa",
        "users.delete",
        "users.manage-roles",
        "roles.read",
        "roles.manage"
      ]
    }
  ],
  "DefaultUserRoles": ["StandardUser"],
  "DefaultAdminRoles": ["IdentityAdmin"]
}
```

**Behaviour**
- At startup the Roles package seeds roles based on definitions absent in the database.
- `DefaultUserRoles` apply to users created via self-registration.
- `DefaultAdminRoles` apply to users created via admin flows or seed scripts when flagged as admins.
- Consumers can override or extend these arrays per environment.

### Audit configuration
```jsonc
"Audit": {
  "Enabled": true
}
```
- When `false`, admin endpoints still execute but skip writing `AuditEntry` rows.
- When `true`, every admin operation (and optionally select end-user operations) writes an audit record.

---

## API Touchpoints

- `GET /users/me/permissions` (Roles package) – returns the flattened list of permissions derived from the user’s roles. When the caller includes `X-Organization-Id`, the organization package’s additional permission source runs first and the response also contains the active organization’s permissions (intersection of org roles + user membership); omit the header to see only global RBAC permissions.
- Admin API will rely on permissions in JWT claims to authorize CRUD of users/roles.

Sample response:
```json
{
  "permissions": [
    "users.read",
    "users.lock"
  ]
}
```

---

## Audit Logging Guidelines
- Actor is determined via `UserManager`/current principal.
- `Action` should map to permission identifiers when applicable (e.g., `users.lock`).
- `Metadata` JSON includes relevant context (reason, role changes, etc.).
- Consider building indexes on `ActorUserId`, `TargetUserId`, and `CreatedAt` for reporting.
- Future enhancement: pluggable sinks (e.g., push to message bus). For now, DB storage suffices.

---

## Next Steps
Use this document when implementing:
- EF migrations generated from the consuming host (`IdentityRolesDbContext`).
- Configuration binding + seeding.
- Admin endpoint authorization checks.
- Documentation references in public guides.
