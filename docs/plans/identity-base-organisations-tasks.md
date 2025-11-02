# Identity Base Organisations – Task Breakdown

## P0 – Foundation & Project Scaffold *(High Priority)*

### Task P0.1 – Create Package Structure & Project Files
- Scaffold the `Identity.Base.Organisations` project directory with the folders outlined in the plan (Abstractions, Data, Domain, Options, Services, Infrastructure, Api, Extensions, README, csproj).
- Configure `Identity.Base.Organisations.csproj` with NuGet metadata (authors, license, repository URL, tags) consistent with existing packages.
- Add project references to `Identity.Base` and `Identity.Base.Roles`; include EF Core/Npgsql/Microsoft.Extensions packages mirroring RBAC project versions.
- Update `Identity.sln` to include the new project and ensure builds via `dotnet build Identity.sln` succeed.

### Task P0.2 – Scaffold Test Project
- Create `Identity.Base.Organisations.Tests` with xUnit setup (add to solution and `dotnet test` target).
- Reference `Identity.Base.Organisations` and any required test utilities (e.g., Microsoft.AspNetCore.Mvc.Testing for integration tests later).
- Add placeholder tests (e.g., verifying DI registration) to confirm the test project runs.

### Task P0.3 – Update Shared Build/Dependency Files
- If repository uses shared props/targets, ensure new project is included (e.g., coverage filters, packaging settings).
- Update CI/test scripts if necessary to include the new project.

## P1 – Domain Model & EF Core *(High Priority)*

### Task P1.1 – Implement Domain Entities & Value Objects
- Create `Organisation`, `OrganisationMembership`, `OrganisationRole`, `OrganisationRoleAssignment`, and `OrganisationMetadata` classes in the Domain folder.
- Ensure domain modelling matches the plan: optional `TenantId`, slug constraints, metadata JSON, timestamps.
- Add enum(s) or value objects for organisation status (e.g., Active, Archived).

### Task P1.2 – Configure EF Core Model
- Implement `OrganisationDbContext` with DbSet properties, schema mapping (table names/prefix), and JSONB metadata configuration.
- Create entity type configuration classes under Data/Configurations for each entity (indexes, relationships, property constraints).
- Ensure context consumes Identity Base model customization hook (`ConfigureOrganisationModel`).

### Task P1.3 – Provide EF Core Infrastructure
- Add migrations folder and initial migration (PostgreSQL provider) generating all required tables.
- Implement design-time factory for CLI usage.
- Write `OrganisationMigrationHostedService` to apply migrations at startup.

## P2 – Services & Seeders *(High Priority)*

### Task P2.1 – Organisation Service Layer
- Implement `OrganisationService` with CRUD (create, update, archive) and metadata updates.
- Implement `OrganisationMembershipService` with add/remove member, update roles, set primary org, list memberships.
- Implement `OrganisationRoleService` to manage org roles and map permissions (leveraging RBAC abstractions).
- Incorporate validation (guard against duplicate slugs, cross-tenant leakage, etc.).

### Task P2.2 – Seeders & Hosted Services
- Implement `OrganisationRoleSeeder` to seed default org roles (`OrgOwner`, `OrgManager`, `OrgMember`) and tie into hosted seed service.
- Implement `OrganisationSeedHostedService` to run seeding logic at startup.
- Register these in DI (via extensions) and utilize `IdentityBaseSeedCallbacks.AfterOrganisationSeed` to allow additional actions.

### Task P2.3 – Context Accessors & Claim Integration
- Create `OrganisationContextAccessor` and `DefaultOrganisationContextAccessor` mirroring tenant pattern.
- Implement `OrganisationClaimFormatter` to emit org context when augmenting claims.
- Implement `OrganisationScopeResolver`; default behavior enforces membership while remaining easy to override for custom scopes.

## P3 – Dependency Injection & Builder Extensions *(High Priority)*

### Task P3.1 – ServiceCollection Extensions
- Implement `ServiceCollectionExtensions.AddIdentityBaseOrganisations` to register DbContext (with options overload), services, hosted services, context accessor, claim formatter, scope resolver.
- Provide overloads to allow consumers to configure DbContext externally.

### Task P3.2 – IdentityBaseBuilder Extensions
- Implement extension methods on `IdentityBaseBuilder` (in `IdentityBaseBuilderOrganisationsExtensions`) for:
  - `ConfigureOrganisationModel`
  - `AfterOrganisationSeed`
  - `AddOrganisationClaimFormatter<T>()`
  - `AddOrganisationScopeResolver<T>()`
- Ensure methods chain and return builder for fluent use.

### Task P3.3 – Documentation Comments & Guards
- Add XML comments for public APIs to help consumers; ensure guard clauses throw informative exceptions.

## P4 – HTTP API Layer *(High Priority)*

### Task P4.1 – DTOs & Validators
- Create request/response DTOs in `Api/Models` for organisations, memberships, roles.
- Implement validation using FluentValidation (if used in existing code) or manual guard logic.

### Task P4.2 – Minimal API Modules
- Implement endpoint modules (e.g., `OrganisationEndpoints`) registering routes for CRUD, membership, roles, and user-centric endpoints.
- Apply authorization policies using RBAC (`PermissionRequirement`); define required permission strings.
- Integrate `IOrganisationContextAccessor` for active org operations (e.g., setting active org for current user).

### Task P4.3 – API Wiring & Service Registration
- Provide extension method to map organisation endpoints (e.g., `app.MapIdentityBaseOrganisationEndpoints()`).
- Update README with instructions for registering endpoints in consumer hosts.

## P5 – Testing *(High Priority)*

### Task P5.1 – Unit Tests
- Add unit tests for services (organisation CRUD, membership, roles) using InMemory DbContext or test DB.
- Test claim formatter & scope resolver behavior.
- Cover seeders (ensure defaults seeded, no duplicates on rerun).

### Task P5.2 – Integration Tests
- Use `WebApplicationFactory` to host Minimal API endpoints; test success/failure scenarios with InMemory provider.
- Verify authorization behavior (permissions required) using stub policies or test-specific configuration.

### Task P5.3 – Regression Tests for Builder Hooks
- Add tests ensuring builder extensions register delegates and seed callbacks properly (similar to existing `ModelCustomizationTests`).
- Ensure `AfterOrganisationSeed` is invoked after seeding.

## P6 – Documentation & Samples *(Medium Priority)*

### Task P6.1 – README & Usage Guide
- Write README covering package overview, installation, configuration steps, sample code for service registration, and API usage (with curl examples or HTTPie).

### Task P6.2 – Repository Docs
- Update `docs/guides/getting-started.md` to mention optional organisation add-on and highlight relevant builder hooks.
- Update `docs/reference/Engineering_Principles.md` to include organisation extensibility note.
- Update `docs/multi-tenant-implementation-plan.md` referencing new OSS organisations plan.

### Task P6.3 – CHANGELOG Entry
- Add entry under Unreleased describing addition of `Identity.Base.Organisations` package and key features.

## P7 – Packaging & Release *(Medium Priority)*

### Task P7.1 – NuGet Packaging Prep
- Ensure `.csproj` includes package description, README, license metadata, and icon (if used by other packages).
- Verify pack command produces expected artifacts (`dotnet pack`).

### Task P7.2 – CI Integration
- Update CI workflow to build/test new projects.
- Optionally add pack/publish steps or ensure manual release instructions cover the new package.

### Task P7.3 – Sample Showcase (Optional)
- Add minimal sample instructions (or sample project) demonstrating how to enable organisations in an app (if time allows).

---

### Prioritization Summary
- **P0–P5**: Must complete before OSS package release (core functionality, tests).
- **P6–P7**: Documentation and packaging work; deliver immediately after core to finish release.
- Optional tasks (samples, advanced scenarios) can follow once main package is stable.
