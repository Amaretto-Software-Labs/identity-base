# Identity Base Organizations – Task Breakdown

## P0 – Foundation & Project Scaffold *(High Priority)*

### Task P0.1 – Create Package Structure & Project Files
- Scaffold the `Identity.Base.Organizations` project directory with the folders outlined in the plan (Abstractions, Data, Domain, Options, Services, Infrastructure, Api, Extensions, README, csproj).
- Configure `Identity.Base.Organizations.csproj` with NuGet metadata (authors, license, repository URL, tags) consistent with existing packages.
- Add project references to `Identity.Base` and `Identity.Base.Roles`; include EF Core/Npgsql/Microsoft.Extensions packages mirroring RBAC project versions.
- Update `Identity.sln` to include the new project and ensure builds via `dotnet build Identity.sln` succeed.

### Task P0.2 – Scaffold Test Project
- Create `Identity.Base.Organizations.Tests` with xUnit setup (add to solution and `dotnet test` target).
- Reference `Identity.Base.Organizations` and any required test utilities (e.g., Microsoft.AspNetCore.Mvc.Testing for integration tests later).
- Add placeholder tests (e.g., verifying DI registration) to confirm the test project runs.

### Task P0.3 – Update Shared Build/Dependency Files
- If repository uses shared props/targets, ensure new project is included (e.g., coverage filters, packaging settings).
- Update CI/test scripts if necessary to include the new project.

## P1 – Domain Model & EF Core *(High Priority)*

### Task P1.1 – Implement Domain Entities & Value Objects
- Create `Organization`, `OrganizationMembership`, `OrganizationRole`, `OrganizationRoleAssignment`, and `OrganizationMetadata` classes in the Domain folder.
- Ensure domain modelling matches the plan: optional `TenantId`, slug constraints, metadata JSON, timestamps.
- Add enum(s) or value objects for organization status (e.g., Active, Archived).

### Task P1.2 – Configure EF Core Model
- Implement `OrganizationDbContext` with DbSet properties, schema mapping (table names/prefix), and JSONB metadata configuration.
- Create entity type configuration classes under Data/Configurations for each entity (indexes, relationships, property constraints).
- Ensure context consumes Identity Base model customization hook (`ConfigureOrganizationModel`).

### Task P1.3 – Provide EF Core Infrastructure
- Add migrations folder and initial migration (PostgreSQL provider) generating all required tables.
- Implement design-time factory for CLI usage.
- Write `OrganizationMigrationHostedService` to apply migrations at startup.

## P2 – Services & Seeders *(High Priority)*

### Task P2.1 – Organization Service Layer
- Implement `OrganizationService` with CRUD (create, update, archive) and metadata updates.
- Implement `OrganizationMembershipService` with add/remove member, update roles, set primary org, list memberships.
- Implement `OrganizationRoleService` to manage org roles and map permissions (leveraging RBAC abstractions).
- Incorporate validation (guard against duplicate slugs, cross-tenant leakage, etc.).

### Task P2.2 – Seeders & Hosted Services
- Implement `OrganizationRoleSeeder` to seed default org roles (`OrgOwner`, `OrgManager`, `OrgMember`) and tie into hosted seed service.
- Implement `OrganizationSeedHostedService` to run seeding logic at startup.
- Register these in DI (via extensions) and utilize `IdentityBaseSeedCallbacks.AfterOrganizationSeed` to allow additional actions.

### Task P2.3 – Context Accessors & Claim Integration
- Create `OrganizationContextAccessor` and `DefaultOrganizationContextAccessor` mirroring tenant pattern.
- Implement `OrganizationClaimFormatter` to emit org context when augmenting claims.
- Implement `OrganizationScopeResolver`; default behavior is permissive (returns true) but structure to allow overrides.

## P3 – Dependency Injection & Builder Extensions *(High Priority)*

### Task P3.1 – ServiceCollection Extensions
- Implement `ServiceCollectionExtensions.AddIdentityBaseOrganizations` to register DbContext (with options overload), services, hosted services, context accessor, claim formatter, scope resolver.
- Provide overloads to allow consumers to configure DbContext externally.

### Task P3.2 – IdentityBaseBuilder Extensions
- Implement extension methods on `IdentityBaseBuilder` (in `IdentityBaseBuilderOrganizationsExtensions`) for:
  - `ConfigureOrganizationModel`
  - `AfterOrganizationSeed`
  - `AddOrganizationClaimFormatter<T>()`
  - `AddOrganizationScopeResolver<T>()`
- Ensure methods chain and return builder for fluent use.

### Task P3.3 – Documentation Comments & Guards
- Add XML comments for public APIs to help consumers; ensure guard clauses throw informative exceptions.

## P4 – HTTP API Layer *(High Priority)*

### Task P4.1 – DTOs & Validators
- Create request/response DTOs in `Api/Models` for organizations, memberships, roles.
- Implement validation using FluentValidation (if used in existing code) or manual guard logic.

### Task P4.2 – Minimal API Modules
- Implement endpoint modules (e.g., `OrganizationEndpoints`) registering routes for CRUD, membership, roles, and user-centric endpoints.
- Apply authorization policies using RBAC (`PermissionRequirement`); define required permission strings.
- Integrate `IOrganizationContextAccessor` for active org operations (e.g., setting active org for current user).

### Task P4.3 – API Wiring & Service Registration
- Provide extension method to map organization endpoints (e.g., `app.MapIdentityBaseOrganizationEndpoints()`).
- Update README with instructions for registering endpoints in consumer hosts.

## P5 – Testing *(High Priority)*

### Task P5.1 – Unit Tests
- Add unit tests for services (organization CRUD, membership, roles) using InMemory DbContext or test DB.
- Test claim formatter & scope resolver behavior.
- Cover seeders (ensure defaults seeded, no duplicates on rerun).

### Task P5.2 – Integration Tests
- Use `WebApplicationFactory` to host Minimal API endpoints; test success/failure scenarios with InMemory provider.
- Verify authorization behavior (permissions required) using stub policies or test-specific configuration.

### Task P5.3 – Regression Tests for Builder Hooks
- Add tests ensuring builder extensions register delegates and seed callbacks properly (similar to existing `ModelCustomizationTests`).
- Ensure `AfterOrganizationSeed` is invoked after seeding.

## P6 – Documentation & Samples *(Medium Priority)*

### Task P6.1 – README & Usage Guide
- Write README covering package overview, installation, configuration steps, sample code for service registration, and API usage (with curl examples or HTTPie).

### Task P6.2 – Repository Docs
- Update `docs/guides/getting-started.md` to mention optional organization add-on and highlight relevant builder hooks.
- Update `docs/reference/Engineering_Principles.md` to include organization extensibility note.
- Update `docs/multi-tenant-implementation-plan.md` referencing new OSS organizations plan.

### Task P6.3 – CHANGELOG Entry
- Add entry under Unreleased describing addition of `Identity.Base.Organizations` package and key features.

## P7 – Packaging & Release *(Medium Priority)*

### Task P7.1 – NuGet Packaging Prep
- Ensure `.csproj` includes package description, README, license metadata, and icon (if used by other packages).
- Verify pack command produces expected artifacts (`dotnet pack`).

### Task P7.2 – CI Integration
- Update CI workflow to build/test new projects.
- Optionally add pack/publish steps or ensure manual release instructions cover the new package.

### Task P7.3 – Sample Showcase (Optional)
- Add minimal sample instructions (or sample project) demonstrating how to enable organizations in an app (if time allows).

---

### Prioritization Summary
- **P0–P5**: Must complete before OSS package release (core functionality, tests).
- **P6–P7**: Documentation and packaging work; deliver immediately after core to finish release.
- Optional tasks (samples, advanced scenarios) can follow once main package is stable.
