# Changelog

## [Unreleased]
- Breaking: removed built-in external provider registration helpers from `Identity.Base` (`AddConfiguredExternalProviders()`, `AddGoogleAuth(...)`, `AddMicrosoftAuth(...)`, `AddAppleAuth(...)`).
- External authentication is now host-driven and provider-agnostic: hosts must register schemes and map route keys with `AddExternalAuthProvider(provider, scheme, ...)`.
- External provider resolution now enforces explicit allowlisting via registered provider mappings; unregistered `/auth/external/{provider}` keys return `400 Unknown external provider`.
- Removed the core `externalProviders` health check registration and removed `ExternalProviders` appsettings binding from the default `AddIdentityBase` option wiring.
- Updated host/sample wiring and docs to reflect the new model, and added migration guidance in `docs/guides/migration-external-provider-registration.md`.
- Fixed organization-role seeding on relational databases by creating a system anchor organization for `OrganizationId = Guid.Empty` before default global roles are inserted (prevents FK violations in hosts such as `apps/org-sample-api`).
- `Identity.Base.Host` now supports optional Google external auth registration via `Authentication:Google` configuration (`Enabled`, provider key, scheme, callback path, client credentials).
- OpenIddict seeding now normalizes legacy permission/requirement prefixes (`endpoints:*`, `grant_types:*`, `response_types:*`, `scope(s):*`, `requirements:*`) to OpenIddict canonical values (`ept:*`, `gt:*`, `rst:*`, `scp:*`, `ft:*`) to avoid unauthorized-client regressions.
- External unlink now rejects removal of the last available sign-in method (external-only users must set a password or link another provider first).
- Added `Authentication:External` options for external-login association policy:
  - `AutoLinkByEmailOnLogin` (default `true`) controls whether callbacks may auto-associate to existing users by matching email.
  - `RequireVerifiedEmailForAutoLinkByEmail` (default `false`) requires an explicit verified-email claim before email-based auto-linking.

## [0.7.18] - 2026-01-25
- OpenIddict seeding is now strictly configuration-driven (no implicit permissions or requirements). Scope permissions accept `scope:` and `scopes:` prefixes and are normalized to `scp:`.

## [0.7.16] - 2026-01-20
- Password validation now follows ASP.NET Core Identity options (removing the separate, conflicting password settings).

## [0.7.15] - 2026-01-06
- Added permission claim utilities in `Identity.Base.Roles` plus org claim helpers in `Identity.Base.Organizations` for consistent server-side permission checks.

## [0.7.12] - 2025-12-30
- Angular client packages now ship ng-packagr/Ivy partial compilation output for AOT compliance, plus a new `IdentityRequireAuthGuard` and improved auth loading/error handling.
- `/auth/*` endpoints now enforce browser Origin checks against `Cors:AllowedOrigins` to prevent cross-origin cookie abuse.
- Authentication cookies now use `SameSite=Lax` to scope session cookies to the Identity host (SPAs should rely on access tokens).
- Added comprehensive test coverage for admin permissions and MailJet/SendGrid email sender behavior, plus new login/origin blocking tests.
- Sample apps and docs updated for new local ports and origin configuration.
- CI now produces and uploads coverage reports.

## [0.7.9] - 2025-12-17
- Removed OAuth2 password grant (ROPC) support end-to-end (server config, seeding, tests, and docs) in favor of authorization-code + PKCE.
- No-op email sender now logs only in Development and emits a Production warning when invoked.
- OpenIddict development signing/encryption keys are now blocked outside Development.
- Admin user listing role filter no longer relies on cross-DbContext LINQ.
- Anonymous invitation lookup now returns a public preview DTO (no invitee email/role ids).
- Added regex match timeouts for configurable profile field patterns and slug validation.
- Replaced `Mailjet.Api` with a lightweight `HttpClient` Mailjet implementation; resolved vulnerable transitive packages.
- Added `Identity.Base.Email.SendGrid`, a SendGrid-backed `ITemplatedEmailSender` package (options, health check, and DI wiring).
- Removed legacy `Microsoft.AspNetCore.Routing.Abstractions` package references in favor of `Microsoft.AspNetCore.App`.
- React/JS client defaults: token storage now defaults to `sessionStorage`, and `createError()` preserves `Error.message`.
- Added missing projects to `Identity.sln` (host migration assemblies + `apps/sample-api`).
- Added new npm packages: `@identity-base/client-core` (framework-agnostic), `@identity-base/angular-client` (Angular DI + interceptor), and `@identity-base/angular-organizations` (organization client for Angular).
- React and Angular clients now depend on `@identity-base/client-core` (no separate install required) and the React client reuses core implementations instead of duplicating them.

## [0.7.7] - 2025-12-04
- Updated EF Core models/configuration to be database-provider agnostic (removed provider-specific JSON column types/defaults) and added provider compatibility tests.
- `TenantId` is now non-nullable across organization entities; host migrations update existing NULL values to `Guid.Empty`.
- Sample host gained initial SQL Server migrations plus dynamic provider/migrations assembly selection via configuration.

## [0.7.6] - 2025-11-26
- Added OpenIddict client credentials flow support (validator, grant handler, per-client config + seeding) with integration tests.
- CI now uses `dotnet tool update` for `nbgv` (Nerdbank.GitVersioning), falling back to install when needed.
- Added a guide for adding permissions to organization roles.

## [0.7.5] - 2025-11-14
- MailJet template ID resolution now supports numeric template keys and logs warnings for invalid/unconfigured keys.
- Removed default email subjects from the built-in account confirmation, password reset, and email MFA notification contexts.

## [0.7.4] - 2025-11-13
- Consolidated lifecycle handling:
  - Introduced `IUserLifecycleListener` + `IUserLifecycleHookDispatcher` with before/after hooks for every user event (self/admin registration, email confirmation, password reset/change, MFA enable/disable/recovery, resend confirmation, forgot password, lock/unlock, role updates, delete/restore). Minimal APIs now build `UserLifecycleContext` instances and run through the dispatcher; `AccountEmailService`, MFA endpoints, and admin endpoints all emit lifecycle events. Legacy `IUser*Listener` implementations continue to work through the shim.
  - Added notification context augmentors (`INotificationContextAugmentor<TContext>`) and pipelines for confirmation/password/MFA emails so hosts can mutate template keys/variables/channels before delivery.
  - Extended organization lifecycle with a single `IOrganizationLifecycleListener` plus dispatcher covering organization create/update/archive/restore, invitation created/revoked/accepted, and membership add/update/remove. Legacy organization listeners map through a compatibility adapter.
- Documentation now highlights the new lifecycle APIs (Identity Base + Organizations package references), including the builder helpers and the plan doc for deeper guidance.

## [0.7.3] - 2025-11-10
- Introduced `IOrganizationCreationListener` and `AddOrganizationCreationListener<T>()` so hosts can hook into organization creation events (billing, automation, audit) without modifying core services.
- Refresh-token flow now re-applies organization membership claims (new `RefreshTokenAugmentorHandler`), and the React org client skips `X-Organization-Id` on user routes to avoid 403s immediately after org creation. Added integration tests illustrating the pre-refresh failure and safe post-refresh/user-route behavior.
- Added user lifecycle listeners (`IUserCreationListener`, `IUserUpdateListener`, `IUserDeletionListener`, `IUserRestoreListener`) and builder helpers; profile and admin endpoints now invoke them, with new integration tests covering update/delete/restore hooks.

## [0.7.2] - 2025-11-09
- React Organizations client now exposes explicit route namespaces and removes generic methods (breaking): use `client.user.*` for `/users/me/organizations/...` and `client.admin.*` for `/admin/organizations/...`. The previous top-level methods like `getOrganization`, `listMembers`, `updateMember`, `listRoles`, `getRolePermissions`, `listInvitations` were removed.
- React Identity client (@identity-base/react-client) now exposes admin APIs under `authManager.admin.*` (breaking): top-level admin methods were removed. Migrate usages such as `listAdminUsers()` → `admin.users.list()`, `createAdminRole()` → `admin.roles.create()`, and `listAdminPermissions()` → `admin.permissions.list()`.
- Sample apps updated to prefer user-scoped organization routes; `org-sample-client` switched config and API calls to `/users/me/organizations/{id}/...` for organization details, members, roles, and role permissions.
- Added a migration guide describing the 0.7.1 → 0.7.2 API changes and code examples.

## [0.7.1] - 2025-11-08
- Identity.Base, Admin, Roles, and Organizations no longer ship EF Core migrations or provider-specific `DatabaseOptions`; hosts now pass a `configureDbContext` delegate (or register the contexts directly) so consumers can target PostgreSQL, SQL Server, or any EF provider.
- Added `IdentityDbNamingOptions`/`UseTablePrefix` plus shared naming helpers so every table/index can adopt a host-defined prefix (the built-in default remains `Identity_`).
- Sample hosts (`Identity.Base.Host`, `apps/org-sample-api`, `apps/sample-api`) gained provider helper extensions, design-time factories, and their own migrations that demonstrate per-host prefixes (`Host_*`, `OrgSample_*`); startup migrators were updated to run the host-generated migrations before seeding.
- Updated tests, docs, and helper APIs to remove the old migration services, ensure DbContexts throw when unconfigured, and silence EF’s `ManyServiceProvidersCreatedWarning` during in-memory runs.

## [0.6.3] - 2025-11-08
- Reintroduced `POST /users/me/organizations`, allowing authenticated users to create an organization that immediately assigns them the default OrgOwner role (respects custom `Organizations:RoleOptions` seeds).
- Removed the legacy `IsPrimary` membership concept. Membership DTOs/queries no longer expose the flag, list endpoints dropped the `isPrimary` filter, and React + sample clients were updated accordingly. Organization context must now be set explicitly via `X-Organization-Id`.
- Organization authorization now enforces a configurable `Organizations:Authorization:AdminRequiredScope` (default `identity.admin`) before honoring `admin.organizations.*` permissions, so admin endpoints reject tokens missing the privileged scope.
- Doc updates: clarified that `/users/me/permissions` only merges organization permissions when the `X-Organization-Id` header is present, and omitting the header yields just the global RBAC set.
- Added a shared NuGet icon configuration (`assets/images/logo-white-128x128.png`), so every published package now advertises the logo in the gallery.

## [0.6.2] - 2025-11-07
- Added the full `/users/me/organizations/{orgId}/...` management surface (details, patch, members, roles, invitations) guarded by the `user.organizations.*` permissions so organization owners can self-service memberships/roles using the same pagination/authorization model as the admin APIs.
- Introduced shared pagination helpers (`PageRequest`, `PagedResult`, `SortExpression`) and moved the `/users/me/organizations` list to the new contract (paging, filtering, sorting, optional archived results).
- Added paged queries to `IOrganizationService`, `IOrganizationRoleService`, and `OrganizationInvitationService` (plus the EF stores), refreshed the sample React clients (`identity-react-client`, `identity-react-organizations`, `org-sample-client`), and expanded the test suite to cover the new list semantics.

## [0.6.1] - 2025-11-04
- Renamed the legacy `organization.*` permissions to `admin.organizations.*` across APIs and samples, and seeded parallel `user.organizations.*` entries on default organization roles so org-scoped flows can evolve independently.
- Tokens now include an `org:memberships` claim and the new `UseOrganizationContextFromHeader()` middleware honors the `X-Organization-Id` header, eliminating the need to refresh tokens when switching organizations (only membership changes require a refresh).
- Relocated organization CRUD, membership, role, and invitation endpoints beneath `/admin/organizations/...` and removed the `/users/me/organizations/active` endpoint so callers set context exclusively via the `X-Organization-Id` header; updated docs/playbooks accordingly.
- Completed the pagination rollout to every admin list endpoint: `/admin/users`, `/admin/roles`, `/admin/permissions`, `/admin/organizations`, and the nested `/admin/organizations/{id}/members|roles|invitations` routes now emit `PagedResult<T>` responses, accept unified `page/pageSize/search/sort` parameters, and rely on the shared sorting helpers in both EF services and Minimal APIs.

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
