# Identity Base Organizations Add-On Plan

## Overview

`Identity.Base.Organizations` will be an OSS NuGet package that layers full organization management on top of the existing Identity Base core and RBAC packages. It introduces organization entities, membership, and role abstractions so applications can group users inside a tenant (or standalone installation) while reusing the same authorization primitives. The package must ship complete domain, data access, services, hosted migration/seeding jobs, and HTTP APIs, enabling consumers to adopt it without additional scaffolding.

Tenant support remains a premium/commercial capability. The OSS organizations package must operate independently of the tenant abstraction while exposing hooks that the commercial add-on can compose later.

## Goals

1. Deliver a self-contained OSS package (`Identity.Base.Organizations`) with:
   - EF Core domain model, migrations, and DI extensions
   - Organization, membership, and role services
   - Default claim formatter/scope resolver integrations with RBAC
   - Minimal API endpoints for CRUD, membership, role management, and user-focused operations
2. Expose organization context accessors and builder hooks so downstream apps can plug organization awareness into authentication/authorization flows.
3. Keep Identity Base core unchanged apart from leveraging existing extensibility points (tenant context, EF model hooks, seed callbacks, claim formatter interfaces).
4. Ensure the package is production-ready (tests, docs, configuration guidance) and publishable to NuGet.
5. Prepare for commercial reuse: the premium tenant add-on should decorate/compose the organization package with tenant-aware behaviors without modifying OSS code.

## Deliverables

### 1. Package Structure

```
Identity.Base.Organizations/
  Abstractions/
    IOrganizationContextAccessor.cs
    IOrganizationService.cs
    IOrganizationMembershipService.cs
    IOrganizationRoleService.cs
  Data/
    OrganizationDbContext.cs
    Configurations/
    Migrations/
  Domain/
    Organization.cs
    OrganizationMembership.cs
    OrganizationRole.cs
    OrganizationRoleAssignment.cs
    OrganizationMetadata.cs
  Options/
    OrganizationOptions.cs
    OrganizationRoleOptions.cs
  Services/
    OrganizationService.cs
    OrganizationMembershipService.cs
    OrganizationRoleService.cs
    OrganizationContextAccessor.cs
    DefaultOrganizationContextAccessor.cs
    OrganizationRoleSeeder.cs
    OrganizationClaimFormatter.cs
    OrganizationScopeResolver.cs
  Infrastructure/
    OrganizationMigrationHostedService.cs
    OrganizationSeedHostedService.cs
  Api/
    Modules/
      OrganizationEndpoints.cs
      OrganizationMembershipEndpoints.cs
      OrganizationRoleEndpoints.cs
    Models/
      OrganizationDto.cs
      CreateOrganizationRequest.cs
      UpdateOrganizationRequest.cs
      OrganizationMembershipDto.cs
      AddMembershipRequest.cs
      UpdateMembershipRequest.cs
      OrganizationRoleDto.cs
      CreateOrganizationRoleRequest.cs
  Extensions/
    ServiceCollectionExtensions.cs
    IdentityBaseBuilderOrganizationsExtensions.cs
  README.md
  Identity.Base.Organizations.csproj
```

- Target framework: `net9.0`.
- Project references: `Identity.Base`, `Identity.Base.Roles`.
- Package metadata aligned with existing OSS packages (authors, license, repository URL, readme).

### 2. Domain Model

| Entity | Description |
| --- | --- |
| `Organization` | Represents a logical grouping of users. Fields: `Id`, optional `TenantId`, `Slug`, `DisplayName`, `Status`, `Metadata (JSONB)`, timestamps. Unique index on `(TenantId, Slug)` and `(TenantId, DisplayName)`.
| `OrganizationMembership` | Links users to organizations with optional primary flag. Fields: `OrganizationId`, `UserId`, optional `TenantId`, `IsPrimary`, membership timestamps.
| `OrganizationRole` | Role definition, optionally org-specific or shared across orgs. Fields: `Id`, `OrganizationId?`, `Name`, `Description`, `IsSystemRole`.
| `OrganizationRoleAssignment` | Associates roles with memberships. Fields: `OrganizationId`, `UserId`, `RoleId`, timestamps.
| `OrganizationMetadata` | Value object backing JSON metadata (custom labels, billing references, etc.).

All tables include `TenantId` columns for future commercial composition, but OSS behavior treats `TenantId` as optional.

### 3. EF Core Layer

- `OrganizationDbContext : DbContext`
  - Configures tables with prefix (e.g., `Identity_Organizations`, `Identity_OrganizationMemberships`).
  - Applies entity configurations via `ModelBuilder`.
  - Invokes Identity Base EF customization hooks (`ConfigureOrganizationModel`) to allow consumers to append indexes/constraints.
- Provides EF Core migrations (PostgreSQL default) and design-time factory for CLI tooling.

### 4. Services & DI

- `OrganizationService` – create, update, archive organizations, manage metadata.
- `OrganizationMembershipService` – add/remove members, toggle primary org, list user memberships, enforce basic invariants.
- `OrganizationRoleService` – manage org roles, assign permissions, integrate with `Identity.Base.Roles` APIs.
- `OrganizationRoleSeeder` – seeds default roles (`OrgOwner`, `OrgManager`, `OrgMember`) per organization/tenant.
- `OrganizationContextAccessor` & `DefaultOrganizationContextAccessor` – surfaces the active organization (no-op by default).
- `OrganizationClaimFormatter` – default implementation of `IPermissionClaimFormatter` that adds `org_id`, `org_roles` claims when active.
- `OrganizationScopeResolver` – default implementation of `IPermissionScopeResolver` that enforces organization membership (consumers can override to compose tenant-aware logic or elevated roles).
- `OrganizationMigrationHostedService` / `OrganizationSeedHostedService` – run migrations and seeding during startup.

**DI Extension:**

`services.AddIdentityBaseOrganizations(configuration, options => ...)` should:
- Register the DbContext (overload accepting `Action<IServiceProvider, DbContextOptionsBuilder>`).
- Register services, hosted services, claim formatter, scope resolver, context accessor.
- Wire seed callbacks via `IdentityBaseBuilder.AfterOrganizationSeed` (new builder hook).

### 5. Builder Extension Methods

Add overloads on `IdentityBaseBuilder` via extension class:

- `ConfigureOrganizationModel(Action<ModelBuilder> configure)`
- `AfterOrganizationSeed(Func<IServiceProvider, CancellationToken, Task> callback)`
- `AddOrganizationClaimFormatter<TFormatter>()`
- `AddOrganizationScopeResolver<TResolver>()`

These methods should reuse the `IdentityBaseModelCustomizationOptions` and `IdentityBaseSeedCallbacks` infrastructure already present in core.

### 6. API Surface (Minimal APIs)

Expose Minimal API modules under the `/admin/organizations/...` (global) and member-facing `/organizations/...` namespaces so both administrator and in-organization experiences are covered.

**Admin surface (global tenant/system operators)**

| Endpoint | Description | Auth Requirement |
| --- | --- | --- |
| `GET /admin/organizations` | List organizations visible to the caller (supports paging/filter/sort + optional tenant filters). | `admin.organizations.read` |
| `POST /admin/organizations` | Create new organization. | `admin.organizations.manage` |
| `GET /admin/organizations/{orgId}` | Get organization details. | `admin.organizations.read` |
| `PATCH /admin/organizations/{orgId}` | Update name/metadata/status. | `admin.organizations.manage` |
| `DELETE /admin/organizations/{orgId}` | Archive organization. | `admin.organizations.manage` |
| `GET /admin/organizations/{orgId}/members` | List memberships, roles (supports paging/filter/sort). | `admin.organizations.members.read` |
| `POST /admin/organizations/{orgId}/members` | Add member with roles. | `admin.organizations.members.manage` |
| `PUT /admin/organizations/{orgId}/members/{userId}` | Update member roles/primary flag. | `admin.organizations.members.manage` |
| `DELETE /admin/organizations/{orgId}/members/{userId}` | Remove member. | `admin.organizations.members.manage` |
| `GET /admin/organizations/{orgId}/roles` | List org roles (system + custom, supports paging/filter/sort). | `admin.organizations.roles.read` |
| `POST /admin/organizations/{orgId}/roles` | Create custom role. | `admin.organizations.roles.manage` |
| `DELETE /admin/organizations/{orgId}/roles/{roleId}` | Delete custom role. | `admin.organizations.roles.manage` |
| `GET /admin/organizations/{orgId}/roles/{roleId}/permissions` | Inspect effective vs. explicit permissions. | `admin.organizations.roles.read` |
| `PUT /admin/organizations/{orgId}/roles/{roleId}/permissions` | Replace explicit permission overrides. | `admin.organizations.roles.manage` |
| `GET /admin/organizations/{orgId}/invitations` | List pending invitations (supports paging/filter/sort). | `admin.organizations.members.manage` |
| `POST /admin/organizations/{orgId}/invitations` | Issue invitation without mail delivery. | `admin.organizations.members.manage` |
| `DELETE /admin/organizations/{orgId}/invitations/{code}` | Revoke invitation. | `admin.organizations.members.manage` |

**Member surface (organization owners/managers operating within their org)**

All member endpoints live under `/users/me/organizations/...` to reinforce “self + active org” semantics and avoid conflicting with admin routes.

| Endpoint | Description | Auth Requirement |
| --- | --- | --- |
| `GET /users/me/organizations` | List memberships for the signed-in user (slug, display name, role summary) with paging/filter/sort. | Authenticated |
| `GET /users/me/organizations/{orgId}` | Retrieve organization profile scoped to the caller’s membership. | `user.organizations.read` |
| `PATCH /users/me/organizations/{orgId}` | Update organization display name/metadata (owner/manager only). | `user.organizations.manage` |
| `GET /users/me/organizations/{orgId}/members` | List members within the organization (supports paging/filter/sort). | `user.organizations.members.read` |
| `POST /users/me/organizations/{orgId}/members` | Add an existing user to the organization. | `user.organizations.members.manage` |
| `PUT /users/me/organizations/{orgId}/members/{userId}` | Update roles / primary flag (cannot escalate beyond caller’s permissions). | `user.organizations.members.manage` |
| `DELETE /users/me/organizations/{orgId}/members/{userId}` | Remove member (owner safeguards enforced). | `user.organizations.members.manage` |
| `GET /users/me/organizations/{orgId}/roles` | List organization roles and inherited permissions (supports paging/filter/sort). | `user.organizations.roles.read` |
| `POST /users/me/organizations/{orgId}/roles` | Create organization-specific role (non-system). | `user.organizations.roles.manage` |
| `DELETE /users/me/organizations/{orgId}/roles/{roleId}` | Delete organization-specific role (with safeguards for active assignments). | `user.organizations.roles.manage` |
| `GET /users/me/organizations/{orgId}/roles/{roleId}/permissions` | Show explicit vs. inherited permissions. | `user.organizations.roles.read` |
| `PUT /users/me/organizations/{orgId}/roles/{roleId}/permissions` | Update explicit permission overrides (validated against catalog). | `user.organizations.roles.manage` |
| `GET /users/me/organizations/{orgId}/invitations` | List pending invitations authored by the organization (supports paging/filter/sort). | `user.organizations.members.manage` |
| `POST /users/me/organizations/{orgId}/invitations` | Create invitation (host still handles email delivery). | `user.organizations.members.manage` |
| `DELETE /users/me/organizations/{orgId}/invitations/{code}` | Cancel invitation. | `user.organizations.members.manage` |

Implementation details:
- Use FluentValidation for request DTOs where appropriate (both admin & member surfaces).
- Apply authorization policies via `RequireOrganizationPermission(...)`; member endpoints must ensure callers belong to the organization and enforce owner/manager limits.
- Active org switching remains client-driven via the `X-Organization-Id` header; document refresh requirements after membership/role changes. Member endpoints should automatically scope to the header when provided.
- Member endpoints should gracefully degrade when the RBAC package is absent (e.g., role/permission endpoints disabled when no catalog exists).
- Standardize query parameters for all collection/list endpoints (`page`, `pageSize`, `search`, `sort`, and context-specific filters like `roleId`, `status`, `isPrimary`), and surface them consistently across admin + member APIs.

### 7. Token & Authorization Integration

- Default claim formatter merges org context into existing permissions claim while retaining backwards compatibility.
- Organization scope resolver checks membership (default implementation enforces membership; documentation will show how to extend it for tenant-aware or elevated administrator scenarios).
- Provide helper method to register custom claim formatter/resolver.
- Document how to regenerate tokens after switching org (e.g., call login refresh endpoint).

### 8. Tests

Create `Identity.Base.Organizations.Tests` covering:
- Service behavior (CRUD, membership, roles).
- Authorization (scope resolver, claim formatter).
- API integration tests using `WebApplicationFactory` with InMemory DB to ensure admin and member endpoints enforce policies, honor `X-Organization-Id`, and respect role-based constraints.
- Seed callbacks integration (ensuring `AfterOrganizationSeed` triggers).
- Organization context accessor default behavior.

Update solution to run with `dotnet test Identity.sln` (make sure new test project is included).

### 9. Documentation Updates

- `Identity.Base.Organizations/README.md` – installation, configuration, sample usage, API overview.
- `docs/guides/getting-started.md` – mention optional organization add-on, highlight builder hooks, and clarify admin vs. member endpoints.
- `docs/reference/Engineering_Principles.md` – include note on using public hooks (claim formatter, scope resolver, seed callbacks) for organizations.
- `docs/multi-tenant-implementation-plan.md` – reference org package as the OSS foundation for commercial tenant layering.
- `docs/guides/organization-onboarding-flow.md` & `docs/guides/organization-admin-use-case.md` – document both admin `/admin/organizations/...` and member `/users/me/organizations/...` workflows.
- React/SPA docs (`docs/packages/identity-base-react-organizations/index.md`, sample READMEs) – update client expectations for dual-surface endpoints.
- `CHANGELOG.md` – add entry under Unreleased describing new add-on.

### 10. Build & Packaging

- Update `Identity.sln` to include new project and tests.
- Ensure `.csproj` packages are referenced (EF Core, Npgsql, etc.) consistent with RBAC package.
- Configure `Directory.Build.props` / `Directory.Build.targets` if needed for shared settings.
- Provide NuGet metadata (authors, license, tags `identity;organizations;multitenancy;rbac`).

### 11. Commercial Alignment

- Commercial tenant module will:
  - Implement a tenant-aware `OrganizationContextAccessor` that reads both `TenantId` and `OrganizationId`.
  - Override/extend `OrganizationScopeResolver` to ensure membership + tenant checks.
  - Compose tenant provisioning flows to bootstrap default organization per tenant.
  - Extend portal APIs/UI with tenant filters while relying on OSS endpoints for org CRUD/membership.

No changes are required in OSS beyond the package described, ensuring clean separation: tenants remain commercial, organizations remain OSS.

### 12. Shared Pagination & Sorting (Foundation for Member/Admin Lists)

Before implementing member endpoints, introduce a shared pagination/filtering module so every list surface behaves the same:

- Add `Identity.Base.Abstractions.Pagination` (name TBD) with:
  - `PageRequest` (`Page`, `PageSize`, `Search`, `IReadOnlyList<SortExpression> Sorts`, optional strongly typed filter bag).
  - `SortExpression` struct (`Field`, `Direction` enum).
  - `PagedResult<T>` carrying `Page`, `PageSize`, `TotalCount`, `Items`.
- Provide EF Core helpers (extension methods) that apply `PageRequest` to an `IQueryable<T>`: search pattern escaping, filter callbacks, ordering, and `Skip/Take` with shared max page size rules.
- Define a canonical set of query string parameters (`page`, `pageSize`, `search`, `sort`, plus endpoint-specific filters). Document accepted sort fields per endpoint.
- Adoption plan:
  1. Implement the new `/users/me/organizations/...` list endpoints using the shared module (organizations, members, roles, invitations).
  2. Extend `IOrganizationService`, `IOrganizationRoleService`, `OrganizationInvitationService`/store to accept `PageRequest` inputs.
  3. Update docs/playbooks to reflect the query contract.
  4. Refactor admin endpoints (`/admin/users`, `/admin/roles`, `/admin/organizations/...`) to reuse the shared helpers and return `PagedResult<T>`.
  5. Update React/admin clients after the unified contract is in place.

### 12. Milestones & Suggested Sprints

1. **Sprint Org-1 (OSS Core)**
   - Scaffold project, domain entities, DbContext.
   - Implement services, claim formatter, scope resolver, hosted services.
   - Add DbContext hooks & seed callbacks integration.
   - Unit tests for domain/services.

2. **Sprint Org-2 (APIs & Docs)**
   - Build Minimal APIs and DTOs.
   - Integration tests (WebApplicationFactory).
   - Documentation updates (README, getting started, engineering principles, changelog).
   - NuGet packaging metadata.

3. **Sprint Org-3 (Optional Enhancements)**
   - Additional features (hierarchy, metadata schemas) or advanced policies.
   - Developer samples (e.g., sample app demonstrating organization selector).

After OSS release, commercial sprints compose tenant-aware integrations and portal UX.

---

This plan encapsulates the agreed scope: OSS package with full organization capabilities (domain + API) designed to work standalone and to serve as the foundation for the commercial tenant-aware SaaS offering.
