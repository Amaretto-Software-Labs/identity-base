# Adding Permissions to Organization Roles

This quick guide assumes your host already uses `AddIdentityBase`, `AddIdentityRoles`, `AddIdentityBaseOrganizations`, and runs the seeding helpers on startup (migrations applied for all contexts).

## 1) Add the permission to the catalog

Add your new permission under `Permissions:Definitions` so it exists in the global RBAC table, then run/start the host to let `SeedIdentityRolesAsync` sync it:

```json
"Permissions": {
  "Definitions": [
    { "Name": "reports.read", "Description": "View organization reports" }
  ]
}
```

## 2) Give it to default organization roles (config-driven)

Add the permission to `Organizations:RoleOptions:DefaultRoles`. The organization seed hosted service writes these into `Identity_OrganizationRolePermissions` for the system roles (`OrgOwner`, `OrgManager`, `OrgMember`) when it runs.

```json
"Organizations": {
  "RoleOptions": {
    "DefaultRoles": [
      {
        "Name": "OrgOwner",
        "Permissions": [
          "user.organizations.read",
          "user.organizations.manage",
          "user.organizations.members.read",
          "user.organizations.members.manage",
          "user.organizations.roles.read",
          "user.organizations.roles.manage",
          "reports.read"
        ],
        "DefaultType": "Owner",
        "IsSystemRole": true
      },
      {
        "Name": "OrgManager",
        "Permissions": [
          "user.organizations.read",
          "user.organizations.members.read",
          "user.organizations.members.manage",
          "user.organizations.roles.read",
          "reports.read"
        ],
        "DefaultType": "Manager",
        "IsSystemRole": true
      },
      {
        "Name": "OrgMember",
        "Permissions": [
          "user.organizations.read",
          "reports.read"
        ],
        "DefaultType": "Member",
        "IsSystemRole": true
      }
    ]
  }
}
```

Restart the host (or rerun the seeder) so the org role seed applies the updated permissions.

## 3) Update an existing organization role via API (optional)

If a role already exists and you just need to add the new permission for a specific organization, call the role-permission endpoint:

```bash
curl -X PUT "https://identity.local/admin/organizations/{orgId}/roles/{roleId}/permissions" \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -H "Content-Type: application/json" \
  -d '["reports.read"]'
```

- Admin route requires `admin.organizations.roles.manage`.
- For org-scoped calls by an org admin, use `/users/me/organizations/{orgId}/roles/{roleId}/permissions` with the active `X-Organization-Id` header and `user.organizations.roles.manage`.

## 4) Refresh and verify

- Refresh tokens/cookies after role or permission updates so the aggregated `identity.permissions` claim includes the new value.
- Verify with `GET /users/me/permissions` (include `X-Organization-Id` to see org-scoped permissions) or exercise an endpoint guarded by the new permission.
