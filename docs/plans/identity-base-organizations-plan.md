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
- `OrganizationScopeResolver` – default implementation of `IPermissionScopeResolver` that allows all (consumers can override to enforce membership).
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

Expose Minimal API modules under `/organizations` namespace. Suggested endpoints:

| Endpoint | Description | Auth Requirement |
| --- | --- | --- |
| `GET /organizations` | List organizations visible to the caller (filter by membership or manage permission). | `organizations.read` |
| `POST /organizations` | Create new organization. | `organizations.manage` |
| `GET /organizations/{orgId}` | Get organization details. | `organizations.read` |
| `PATCH /organizations/{orgId}` | Update name/metadata/status. | `organizations.manage` |
| `DELETE /organizations/{orgId}` | Archive organization. | `organizations.manage` |
| `GET /organizations/{orgId}/members` | List memberships, roles. | `organization.members.read` |
| `POST /organizations/{orgId}/members` | Add member with roles. | `organization.members.manage` |
| `PUT /organizations/{orgId}/members/{userId}` | Update member roles/primary flag. | `organization.members.manage` |
| `DELETE /organizations/{orgId}/members/{userId}` | Remove member. | `organization.members.manage` |
| `GET /organizations/{orgId}/roles` | List org roles. | `organization.roles.read` |
| `POST /organizations/{orgId}/roles` | Create custom role. | `organization.roles.manage` |
| `DELETE /organizations/{orgId}/roles/{roleId}` | Delete custom role. | `organization.roles.manage` |
| `GET /users/me/organizations` | List organizations the current user belongs to. | Authenticated |
| `POST /users/me/organizations/active` | Set active organization (`org_id`, `org_key`). | Authenticated |

Implementation details:
- Use FluentValidation for request DTOs where appropriate.
- Apply authorization policies using existing RBAC permissions.
- For active org switching, return payload instructing clients to refresh tokens or optionally issue new Identity cookie with org claims.

### 7. Token & Authorization Integration

- Default claim formatter merges org context into existing permissions claim while retaining backwards compatibility.
- Organization scope resolver checks membership (default implementation returns true to avoid breaking existing apps; documentation will show how to override to enforce membership).
- Provide helper method to register custom claim formatter/resolver.
- Document how to regenerate tokens after switching org (e.g., call login refresh endpoint).

### 8. Tests

Create `Identity.Base.Organizations.Tests` covering:
- Service behavior (CRUD, membership, roles).
- Authorization (scope resolver, claim formatter).
- API integration tests using `WebApplicationFactory` with InMemory DB to ensure endpoints behave and enforce policies.
- Seed callbacks integration (ensuring `AfterOrganizationSeed` triggers).
- Organization context accessor default behavior.

Update solution to run with `dotnet test Identity.sln` (make sure new test project is included).

### 9. Documentation Updates

- `Identity.Base.Organizations/README.md` – installation, configuration, sample usage, API overview.
- `docs/guides/getting-started.md` – mention optional organization add-on and highlight builder hooks.
- `docs/reference/Engineering_Principles.md` – include note on using public hooks (claim formatter, scope resolver, seed callbacks) for organizations.
- `docs/multi-tenant-implementation-plan.md` – reference org package as the OSS foundation for commercial tenant layering.
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
