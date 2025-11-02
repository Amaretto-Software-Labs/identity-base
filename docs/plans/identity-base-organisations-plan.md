# Identity Base Organisations Add-On Plan

## Overview

`Identity.Base.Organisations` will be an OSS NuGet package that layers full organisation management on top of the existing Identity Base core and RBAC packages. It introduces organisation entities, membership, and role abstractions so applications can group users inside a tenant (or standalone installation) while reusing the same authorization primitives. The package must ship complete domain, data access, services, hosted migration/seeding jobs, and HTTP APIs, enabling consumers to adopt it without additional scaffolding.

Tenant support remains a premium/commercial capability. The OSS organisations package must operate independently of the tenant abstraction while exposing hooks that the commercial add-on can compose later.

## Goals

1. Deliver a self-contained OSS package (`Identity.Base.Organisations`) with:
   - EF Core domain model, migrations, and DI extensions
   - Organisation, membership, and role services
   - Default claim formatter/scope resolver integrations with RBAC
   - Minimal API endpoints for CRUD, membership, role management, and user-focused operations
2. Expose organisation context accessors and builder hooks so downstream apps can plug organisation awareness into authentication/authorization flows.
3. Keep Identity Base core unchanged apart from leveraging existing extensibility points (tenant context, EF model hooks, seed callbacks, claim formatter interfaces).
4. Ensure the package is production-ready (tests, docs, configuration guidance) and publishable to NuGet.
5. Prepare for commercial reuse: the premium tenant add-on should decorate/compose the organisation package with tenant-aware behaviors without modifying OSS code.

## Deliverables

### 1. Package Structure

```
Identity.Base.Organisations/
  Abstractions/
    IOrganisationContextAccessor.cs
    IOrganisationService.cs
    IOrganisationMembershipService.cs
    IOrganisationRoleService.cs
  Data/
    OrganisationDbContext.cs
    Configurations/
    Migrations/
  Domain/
    Organisation.cs
    OrganisationMembership.cs
    OrganisationRole.cs
    OrganisationRoleAssignment.cs
    OrganisationMetadata.cs
  Options/
    OrganisationOptions.cs
    OrganisationRoleOptions.cs
  Services/
    OrganisationService.cs
    OrganisationMembershipService.cs
    OrganisationRoleService.cs
    OrganisationContextAccessor.cs
    DefaultOrganisationContextAccessor.cs
    OrganisationRoleSeeder.cs
    OrganisationClaimFormatter.cs
    OrganisationScopeResolver.cs
  Infrastructure/
    OrganisationMigrationHostedService.cs
    OrganisationSeedHostedService.cs
  Api/
    Modules/
      OrganisationEndpoints.cs
      OrganisationMembershipEndpoints.cs
      OrganisationRoleEndpoints.cs
    Models/
      OrganisationDto.cs
      CreateOrganisationRequest.cs
      UpdateOrganisationRequest.cs
      OrganisationMembershipDto.cs
      AddMembershipRequest.cs
      UpdateMembershipRequest.cs
      OrganisationRoleDto.cs
      CreateOrganisationRoleRequest.cs
  Extensions/
    ServiceCollectionExtensions.cs
    IdentityBaseBuilderOrganisationsExtensions.cs
  README.md
  Identity.Base.Organisations.csproj
```

- Target framework: `net9.0`.
- Project references: `Identity.Base`, `Identity.Base.Roles`.
- Package metadata aligned with existing OSS packages (authors, license, repository URL, readme).

### 2. Domain Model

| Entity | Description |
| --- | --- |
| `Organisation` | Represents a logical grouping of users. Fields: `Id`, optional `TenantId`, `Slug`, `DisplayName`, `Status`, `Metadata (JSONB)`, timestamps. Unique index on `(TenantId, Slug)` and `(TenantId, DisplayName)`.
| `OrganisationMembership` | Links users to organisations with optional primary flag. Fields: `OrganisationId`, `UserId`, optional `TenantId`, `IsPrimary`, membership timestamps.
| `OrganisationRole` | Role definition, optionally org-specific or shared across orgs. Fields: `Id`, `OrganisationId?`, `Name`, `Description`, `IsSystemRole`.
| `OrganisationRoleAssignment` | Associates roles with memberships. Fields: `OrganisationId`, `UserId`, `RoleId`, timestamps.
| `OrganisationMetadata` | Value object backing JSON metadata (custom labels, billing references, etc.).

All tables include `TenantId` columns for future commercial composition, but OSS behavior treats `TenantId` as optional.

### 3. EF Core Layer

- `OrganisationDbContext : DbContext`
  - Configures tables with prefix (e.g., `Identity_Organisations`, `Identity_OrganisationMemberships`).
  - Applies entity configurations via `ModelBuilder`.
  - Invokes Identity Base EF customization hooks (`ConfigureOrganisationModel`) to allow consumers to append indexes/constraints.
- Provides EF Core migrations (PostgreSQL default) and design-time factory for CLI tooling.

### 4. Services & DI

- `OrganisationService` – create, update, archive organisations, manage metadata.
- `OrganisationMembershipService` – add/remove members, toggle primary org, list user memberships, enforce basic invariants.
- `OrganisationRoleService` – manage org roles, assign permissions, integrate with `Identity.Base.Roles` APIs.
- `OrganisationRoleSeeder` – seeds default roles (`OrgOwner`, `OrgManager`, `OrgMember`) per organisation/tenant.
- `OrganisationContextAccessor` & `DefaultOrganisationContextAccessor` – surfaces the active organisation (no-op by default).
- `OrganisationClaimFormatter` – default implementation of `IPermissionClaimFormatter` that adds `org_id`, `org_roles` claims when active.
- `OrganisationScopeResolver` – default implementation of `IPermissionScopeResolver` that enforces organisation membership (consumers can override to compose tenant-aware logic or elevated roles).
- `OrganisationMigrationHostedService` / `OrganisationSeedHostedService` – run migrations and seeding during startup.

**DI Extension:**

`services.AddIdentityBaseOrganisations(configuration, options => ...)` should:
- Register the DbContext (overload accepting `Action<IServiceProvider, DbContextOptionsBuilder>`).
- Register services, hosted services, claim formatter, scope resolver, context accessor.
- Wire seed callbacks via `IdentityBaseBuilder.AfterOrganisationSeed` (new builder hook).

### 5. Builder Extension Methods

Add overloads on `IdentityBaseBuilder` via extension class:

- `ConfigureOrganisationModel(Action<ModelBuilder> configure)`
- `AfterOrganisationSeed(Func<IServiceProvider, CancellationToken, Task> callback)`
- `AddOrganisationClaimFormatter<TFormatter>()`
- `AddOrganisationScopeResolver<TResolver>()`

These methods should reuse the `IdentityBaseModelCustomizationOptions` and `IdentityBaseSeedCallbacks` infrastructure already present in core.

### 6. API Surface (Minimal APIs)

Expose Minimal API modules under `/organisations` namespace. Suggested endpoints:

| Endpoint | Description | Auth Requirement |
| --- | --- | --- |
| `GET /organisations` | List organisations visible to the caller (filter by membership or manage permission). | `organisations.read` |
| `POST /organisations` | Create new organisation. | `organisations.manage` |
| `GET /organisations/{orgId}` | Get organisation details. | `organisations.read` |
| `PATCH /organisations/{orgId}` | Update name/metadata/status. | `organisations.manage` |
| `DELETE /organisations/{orgId}` | Archive organisation. | `organisations.manage` |
| `GET /organisations/{orgId}/members` | List memberships, roles. | `organisation.members.read` |
| `POST /organisations/{orgId}/members` | Add member with roles. | `organisation.members.manage` |
| `PUT /organisations/{orgId}/members/{userId}` | Update member roles/primary flag. | `organisation.members.manage` |
| `DELETE /organisations/{orgId}/members/{userId}` | Remove member. | `organisation.members.manage` |
| `GET /organisations/{orgId}/roles` | List org roles. | `organisation.roles.read` |
| `POST /organisations/{orgId}/roles` | Create custom role. | `organisation.roles.manage` |
| `DELETE /organisations/{orgId}/roles/{roleId}` | Delete custom role. | `organisation.roles.manage` |
| `GET /users/me/organisations` | List organisations the current user belongs to. | Authenticated |
| `POST /users/me/organisations/active` | Set active organisation (`org_id`, `org_key`). | Authenticated |

Implementation details:
- Use FluentValidation for request DTOs where appropriate.
- Apply authorization policies using existing RBAC permissions.
- For active org switching, return payload instructing clients to refresh tokens or optionally issue new Identity cookie with org claims.

### 7. Token & Authorization Integration

- Default claim formatter merges org context into existing permissions claim while retaining backwards compatibility.
- Organisation scope resolver checks membership (default implementation enforces membership; documentation will show how to extend it for tenant-aware or elevated administrator scenarios).
- Provide helper method to register custom claim formatter/resolver.
- Document how to regenerate tokens after switching org (e.g., call login refresh endpoint).

### 8. Tests

Create `Identity.Base.Organisations.Tests` covering:
- Service behavior (CRUD, membership, roles).
- Authorization (scope resolver, claim formatter).
- API integration tests using `WebApplicationFactory` with InMemory DB to ensure endpoints behave and enforce policies.
- Seed callbacks integration (ensuring `AfterOrganisationSeed` triggers).
- Organisation context accessor default behavior.

Update solution to run with `dotnet test Identity.sln` (make sure new test project is included).

### 9. Documentation Updates

- `Identity.Base.Organisations/README.md` – installation, configuration, sample usage, API overview.
- `docs/guides/getting-started.md` – mention optional organisation add-on and highlight builder hooks.
- `docs/reference/Engineering_Principles.md` – include note on using public hooks (claim formatter, scope resolver, seed callbacks) for organisations.
- `docs/multi-tenant-implementation-plan.md` – reference org package as the OSS foundation for commercial tenant layering.
- `CHANGELOG.md` – add entry under Unreleased describing new add-on.

### 10. Build & Packaging

- Update `Identity.sln` to include new project and tests.
- Ensure `.csproj` packages are referenced (EF Core, Npgsql, etc.) consistent with RBAC package.
- Configure `Directory.Build.props` / `Directory.Build.targets` if needed for shared settings.
- Provide NuGet metadata (authors, license, tags `identity;organisations;multitenancy;rbac`).

### 11. Commercial Alignment

- Commercial tenant module will:
  - Implement a tenant-aware `OrganisationContextAccessor` that reads both `TenantId` and `OrganisationId`.
  - Override/extend `OrganisationScopeResolver` to ensure membership + tenant checks.
  - Compose tenant provisioning flows to bootstrap default organisation per tenant.
  - Extend portal APIs/UI with tenant filters while relying on OSS endpoints for org CRUD/membership.

No changes are required in OSS beyond the package described, ensuring clean separation: tenants remain commercial, organisations remain OSS.

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
   - Developer samples (e.g., sample app demonstrating organisation selector).

After OSS release, commercial sprints compose tenant-aware integrations and portal UX.

---

This plan encapsulates the agreed scope: OSS package with full organisation capabilities (domain + API) designed to work standalone and to serve as the foundation for the commercial tenant-aware SaaS offering.
