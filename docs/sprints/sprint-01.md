# Sprint 01 – Foundation & Scaffolding

## Focus & Priority
- Establish the baseline solution, folder structure, and engineering guardrails required for further work.
- Priority: **High** (all later sprints depend on this groundwork).

## Streams
- **API Core** – Scaffold minimal API project, configure dependency injection and extension module pattern.
- **Data & Persistence** – Configure PostgreSQL connectivity and EF Core basics.
- **Documentation & Enablement** – Provide initial onboarding docs and ERD placeholders.
- **DevOps & Tooling** – Set up Serilog, testing harness scaffolds, and CI skeleton.

## Stories

### S1-API-001: Scaffold .NET 9 Minimal API Baseline (Priority: High, Stream: API Core)
**Description**
Create the `apps/api` solution with minimal API pattern, feature folders, and shared extension modules as mandated by engineering principles.

**Acceptance Criteria**
- Solution builds with `dotnet build` and exposes a `/healthz` placeholder endpoint.
- `Program.cs` delegates service registration, middleware, and endpoint mapping to extension methods under `/Extensions`.
- Feature folders for `Authentication`, `Users`, `Email` exist with placeholder endpoint files.

**Tasks**
- [ ] Run `dotnet new webapi --use-controllers false` in `apps/api/src`; remove controller remnants and enable minimal API pattern.
- [ ] Create solution (`identity.sln`) and add `apps/api/src` project; set default namespace conventions.
- [ ] Add extension classes (e.g., `ServiceCollectionExtensions`, `EndpointRouteBuilderExtensions`) that expose empty registration methods.
- [ ] Configure `Program.cs` to call extension methods, enable swagger in development, and map `/healthz` endpoint.

**Dependencies**
- None.

### S1-INFRA-002: Establish EF Core & PostgreSQL Infrastructure (Priority: High, Stream: Data & Persistence)
**Description**
Introduce PostgreSQL provider, connection configuration, and initial DbContext placeholder.

**Acceptance Criteria**
- `AppDbContext` class exists extending `DbContext` with placeholder `ApplyConfigurationsFromAssembly` call.
- `appsettings.Development.json` contains connection string pointing to local Postgres; configuration binding via `Options` pattern.
- Database connection validated on startup via scoped health check.

**Tasks**
- [ ] Add packages `Microsoft.EntityFrameworkCore`, `Npgsql.EntityFrameworkCore.PostgreSQL` to API project.
- [ ] Implement configuration class `DatabaseOptions`; bind from `ConnectionStrings:Primary` with validation.
- [ ] Register `AppDbContext` in DI using `UseNpgsql` (snake_case, retry on failure) and add database health check.
- [ ] Document local Postgres setup steps in README stub (credentials, docker compose snippet).

**Dependencies**
- S1-API-001.

### S1-DOCS-003: Documentation & ERD Scaffolding (Priority: Medium, Stream: Documentation & Enablement)
**Description**
Lay down documentation structure to support future work, consistent with engineering principles.

**Acceptance Criteria**
- `/apps/api/docs/README.md` created with architecture overview, prerequisites, and link to engineering principles.
- ERD placeholder directory `/apps/api/docs/erd/` with template Mermaid file.
- Contribution guidelines stub referencing coding standards.

**Tasks**
- [ ] Create README with sections: Overview, Getting Started, Project Structure, Next Steps.
- [ ] Add `erd-template.md` under `/apps/api/docs/erd/` describing how to document aggregates.
- [ ] Draft `/CONTRIBUTING.md` linking to docs and outlining code review expectations.

**Dependencies**
- None.

### S1-DEVOPS-004: Logging & Test Scaffolds (Priority: Medium, Stream: DevOps & Tooling)
**Description**
Introduce Serilog logging baseline and initial test project.

**Acceptance Criteria**
- Serilog configured with console sink and request logging.
- `apps/api/tests` project created with xUnit + FluentAssertions references and sample test verifying `/healthz`.
- CI placeholder workflow documented (GitHub Actions or equivalent) describing build/test steps.

**Tasks**
- [ ] Add Serilog packages; configure in `Program.cs` using `UseSerilog()` with JSON console output and enrichment for environment/service name.
- [ ] Scaffold test project referencing API project; add `WebApplicationFactory` based smoke test.
- [ ] Draft `.github/workflows/ci.yml` (or docs instructions) outlining build/test commands and caching strategy.

**Dependencies**
- S1-API-001.
