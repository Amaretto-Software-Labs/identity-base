# Identity.Base.Roles

Role-based access control primitives for Identity Base. Provides:

- EF Core entities for roles and permissions
- Configuration binding and seeding for default roles
- Services to assign roles to users and resolve effective permissions
- Minimal API endpoints to expose permissions to consumers (`/users/me/permissions`)

This package is consumed by `Identity.Base` and `Identity.Base.Admin` to deliver end-user and administrative RBAC features.
