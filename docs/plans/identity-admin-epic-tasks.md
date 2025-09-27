# Identity Base Admin Epic – Detailed Task Breakdown

This breakdown turns the epic plan into actionable tickets. Suggested ordering follows the phases, but teams can parallelize where sensible. Each item can be treated as a standalone story/task with clear acceptance criteria.

---

## Phase 0 – Foundations & Planning (COMPLETED)

1. **Finalize RBAC domain model** ✅
   - Documented in `docs/reference/rbac-design.md` (entities, permission catalogue).

2. **Configuration blueprint** ✅
   - Sample `Roles:Definitions` and audit toggle captured in the same doc.

3. **Audit trail requirements** ✅
   - AuditEntry schema and guidelines defined in `rbac-design.md`.

4. **Update high-level docs** ✅
   - `identity-base-public-api.md` references roles/audit plan; epic plan links to RBAC design.

---

## Phase 1 – Identity.Base.Roles

1. **Create project skeleton**
   - Add `Identity.Base.Roles` class library to solution.
   - Configure NuGet metadata (PackageId, description, license, README).

2. **Implement EF Core schema**
   - Create entities & configurations:
     - `Role` (Id, Name, Description, IsSystemRole, ConcurrencyStamp).
     - `Permission` (Id, Name, Description).
     - `RolePermission` join.
     - `UserRole` join.
   - Add migrations in `Data/Migrations` folder.
   - Acceptance: `dotnet ef migrations add InitialRoles` builds.

3. **Configuration binding & seeding**
   - Implement options classes to read `Roles:Definitions` & default role config.
   - Write seeding service that creates missing roles/permissions at startup.
   - Acceptance: running host with sample config seeds roles correctly (integration test or manual verification).

4. **Role assignment services**
   - Create application service for assigning/removing roles to a user (with concurrency checks).
   - Implement permission resolution helper (returns deduplicated permissions for a user).
   - Unit tests covering role assignment and resolution.

5. **Default role hook during user creation**
   - Update registration pipeline to assign `Roles:DefaultUserRoles` to new self-service users.
   - Update admin user creation logic to assign `Roles:DefaultAdminRoles` if triggered via seed or API.
   - Tests verifying new users receive expected roles.

6. **`GET /users/me/permissions` endpoint**
   - Add minimal API in `Identity.Base` (or expose via Roles project extension) returning effective permissions.
   - Add integration test verifying auth required + expected payload.

7. **Documentation update**
   - Draft RBAC section in reference doc with example response.
   - Update `docs/guides/getting-started.md` with role config snippet.

---

## Phase 2 – Identity.Base.Admin

1. **Project setup**
   - Add `Identity.Base.Admin` project referencing `Identity.Base.Roles`.
   - Configure NuGet metadata (make it optional add-on).

2. **Authorization plumbing**
   - Implement policy/handler ensuring admin scope and permission claim.
   - Add middleware/filters to translate JWT claims into permission set.
   - Unit tests covering policy behaviour.

3. **`/admin/users` endpoints**
   - Implement list endpoint (paging, filters, sorting).
   - Implement detail endpoint (include roles, MFA status, external logins, audit snippet).
   - Implement create/update endpoints (with validators).
   - Implement lock/unlock, force reset, resend confirmation, MFA reset, soft delete/restore endpoints.
   - Integration tests for each action (incl. forbidden/unauthorized cases).

4. **`/admin/roles` endpoints**
   - Implement CRUD operations for roles (guard against removing system roles or roles in use unless forced).
   - Ensure permissions list comes from `Identity.Base.Roles` definition or database.
   - Integration tests for each endpoint.

5. **Audit logging integration**
   - Extend audit service to write admin events (actor id, target user id, action, metadata).
   - Honour `Audit:Enabled` toggle (no-op when disabled).
   - Tests verifying audit entries created when enabled and skipped when disabled.

6. **Seeding & configuration**
   - Update host seeding routine to ensure admin roles created and assigned to seed admin user(s).
   - Provide sample `appsettings` snippet for admin-specific config (scope, roles).

7. **Changelog entry**
   - Record addition of Roles/Admin packages, new endpoints, configuration steps.

---

## Phase 3 – React Client & Admin UI

1. **React client hooks**
   - In `@identity-base/react-client`, add admin hooks: `useAdminUsers`, `useAdminUser`, `useAdminRoles`, `useAdminUserRoles`.
   - Expose TypeScript types for admin DTOs.
   - Unit tests (mock fetch) ensuring hooks call correct API routes.

2. **Admin layout & routing**
   - Add `/admin` layout with side nav.
   - Protect routes requiring admin permissions (e.g., check `useAuth().userPermissions`).

3. **Users list page**
   - Build table with paging, search filters, status badges.
   - Add actions: view detail, lock/unlock, trigger reset, soft delete.
   - Implement loading/error states.

4. **User detail page**
   - Display profile info, MFA status, external logins, audit snippet.
   - Provide role assignment UI (multi-select).
   - Buttons for admin actions (reset MFA, resend confirmation, restore user).

5. **Roles management page**
   - List roles with permissions count.
   - Modals/forms for create/edit (select permissions from list).
   - Delete role with confirmation (handle in-use warning).

6. **Notifications & confirmations**
   - Add toast notifications for success/failure.
   - Confirmation dialogs for destructive actions.

7. **React tests**
   - Component tests for role assignment form, lock/unlock flow, roles CRUD.
   - Integration test (React Testing Library) verifying permission guard on admin routes.

8. **Documentation updates**
   - Extend React integration guide with admin setup steps, environment variables, sample config.

---

## Phase 4 – Documentation & Release Hardening

1. **Admin operations guide**
   - Write `docs/guides/admin-operations-guide.md` covering admin login, role assignment, audit viewing.

2. **Reference updates**
   - Update API reference with admin endpoints, schemas, permission matrix.

3. **Getting-started & release checklist**
   - Add sections about configuring roles, admin scope, seeding admin accounts.
   - Note audit toggle behaviour.

4. **Postman collection**
   - Create/update Postman collection hitting all admin endpoints with sample payloads.

5. **CI adjustments**
   - Ensure CI builds/tests the new projects, packs both packages, and uploads artifacts.
   - Update release workflow to optionally publish `Identity.Base.Roles` and `Identity.Base.Admin` when triggered.

6. **End-to-end QA pass**
   - Run through admin UI scenarios against a seeded environment.
   - Log any issues and create follow-up tickets.

---

## Phase 5 – Release & Feedback

1. **Publish preview packages**
   - Release prerelease versions (`-alpha`/`-beta`) of Roles & Admin packages.
   - Confirm npm package (React client) built with new hooks.

2. **Changelog & announcements**
   - Finalize changelog entry and internal/external release notes.

3. **Feedback loop**
   - Collect feedback from integrators on admin UI/API.
   - Log backlog items (bulk actions, tenant support, analytics dashboards, etc.).

4. **Post-release tasks**
   - Schedule maintenance stories for bug fixes, improvements.

---

## Notes for Team Planning
- Each numbered item can become a Jira story. Sub-bullets are acceptance criteria/checklist.
- Parallel work: Phase 1 and Phase 2 can overlap once RBAC entities are ready. React work (Phase 3) can start as soon as admin endpoints stabilize.
- Keep docs/tests in sync with implementation to avoid drift.
