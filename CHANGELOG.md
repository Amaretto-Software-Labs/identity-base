# Changelog

## [0.5.0] - 2025-11-02
- Reverted organization package, namespace, and documentation spelling back to American English (`organization`) across the codebase and React SDKs.
- Bumped minor version to 0.5

## [0.4.3] - 2025-11-02
### Highlights
- Release pipeline now stamps React packages, peer dependencies, and NuGet artifacts with the same version (including manual workflow overrides).
- Fixed missing `identity.permissions` claim on authorization-code/hybrid sign-ins by running registered claims augmentors for those flows.
- Renamed all Organization packages, namespaces, endpoints, and React components to use British spelling.
- Modularized Identity Base into reusable libraries and ASP.NET host, adding builder APIs (`AddIdentityBase`, external provider helpers) and EF support.
- Delivered complete auth surface: registration metadata, email confirmation/reset flows, MFA (authenticator, SMS/email via Twilio/Mailjet), external providers, and authorization code PKCE.
- Introduced RBAC (`Identity.Base.Roles`), admin APIs (`Identity.Base.Admin`), and multi-tenant organization management package (`Identity.Base.Organizations`).
- Added optional Mailjet email sender (`Identity.Base.Email.MailJet`), release automation, and refreshed documentation (getting started, full-stack guide, React integration).
- Shipped Docker/docker-compose environment, sample React client harness, and documentation covering deployment, admin operations, headless React integration, and multi-tenant planning.

## Unreleased
- Renamed the legacy `organization.*` permissions to `admin.organizations.*` across APIs and samples, and seeded parallel `user.organizations.*` entries on default organization roles so org-scoped flows can evolve independently.
- Tokens now include an `org:memberships` claim and the new `UseOrganizationContextFromHeader()` middleware honors the `X-Organization-Id` header, eliminating the need to refresh tokens when switching organizations (only membership changes require a refresh).
- Relocated organization CRUD, membership, role, and invitation endpoints beneath `/admin/organizations/...` and removed the `/users/me/organizations/active` endpoint so callers set context exclusively via the `X-Organization-Id` header.
- Updated playbooks, package docs, and integration guides to reflect the `/admin/organizations/...` admin routes, removal of the active-organization endpoint, and header-based organization selection so both humans and agents follow the current flows without confusion.
- Introduced shared pagination helpers (`PageRequest`, `PagedResult`, `SortExpression`) and moved the `/users/me/organizations` list to the new contract (paging, filtering, sorting, optional archived results) to pave the way for consistent list APIs across admin/member surfaces.
- Added the full `/users/me/organizations/{orgId}/...` management surface (details, patch, members, roles, invitations) guarded by the `user.organizations.*` permissions so organization owners can self-service memberships/roles using the same pagination/authorization model as the admin APIs.
- Completed the pagination rollout to every admin list endpoint: `/admin/users`, `/admin/roles`, `/admin/permissions`, `/admin/organizations`, and the nested `/admin/organizations/{id}/members|roles|invitations` routes now emit `PagedResult<T>` responses, accept unified `page/pageSize/search/sort` parameters, and rely on the shared sorting helpers in both EF services and Minimal APIs.
- Added paged queries to `IOrganizationService`, `IOrganizationRoleService`, and `OrganizationInvitationService` (plus the EF stores), refreshed the sample React clients (`identity-client`, `identity-react-organizations`, `org-sample-client`), and expanded the test suite to cover the new list semantics.
