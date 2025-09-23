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
Create the `Identity.Base` project with minimal API pattern, feature folders, and shared extension modules as mandated by engineering principles.
**Status:** Completed

**Acceptance Criteria**
- Solution builds with `dotnet build` and exposes a `/healthz` placeholder endpoint.
- `Program.cs` delegates service registration, middleware, and endpoint mapping to extension methods under `/Extensions`.
- Feature folders for `Authentication`, `Users`, `Email` exist with placeholder endpoint files.

**Tasks**
- [x] Run `dotnet new webapi --use-controllers false` in `Identity.Base`; remove controller remnants and enable minimal API pattern.
- [x] Create solution (`Identity.sln`) and add `Identity.Base/Identity.Base.csproj`; set default namespace conventions.
- [x] Add extension classes (e.g., `ServiceCollectionExtensions`, `EndpointRouteBuilderExtensions`) that expose empty registration methods.
- [x] Configure `Program.cs` to call extension methods, enable swagger in development, and map `/healthz` endpoint.

**Dependencies**
- None.

### S1-INFRA-002: Establish EF Core & PostgreSQL Infrastructure (Priority: High, Stream: Data & Persistence)
**Description**
Introduce PostgreSQL provider, connection configuration, and initial DbContext placeholder.
**Status:** Completed

**Acceptance Criteria**
- `AppDbContext` class exists extending `DbContext` with placeholder `ApplyConfigurationsFromAssembly` call.
- `appsettings.Development.json` contains connection string pointing to local Postgres; configuration binding via `Options` pattern.
- Database connection validated on startup via scoped health check.

**Tasks**
- [x] Add packages `Microsoft.EntityFrameworkCore`, `Npgsql.EntityFrameworkCore.PostgreSQL` to API project.
- [x] Implement configuration class `DatabaseOptions`; bind from `ConnectionStrings:Primary` with validation.
- [x] Register `AppDbContext` in DI using `UseNpgsql` (PascalCase tables with optional `Identity_` prefix, retry on failure) and add database health check.
- [x] Document local Postgres setup steps in README stub (credentials, docker compose snippet).

**Dependencies**
- S1-API-001.

### S1-DOCS-003: Documentation & ERD Scaffolding (Priority: Medium, Stream: Documentation & Enablement)
**Description**
Lay down documentation structure to support future work, consistent with engineering principles.
**Status:** Completed

**Acceptance Criteria**
- `Identity.Base/docs/README.md` created with architecture overview, prerequisites, and link to engineering principles.
- ERD placeholder directory `Identity.Base/docs/erd/` with template Mermaid file.
- Contribution guidelines stub referencing coding standards.

**Tasks**
- [x] Create README with sections: Overview, Getting Started, Project Structure, Next Steps.
- [x] Add `erd-template.md` under `Identity.Base/docs/erd/` describing how to document aggregates.
- [x] Draft `/CONTRIBUTING.md` linking to docs and outlining code review expectations.

**Dependencies**
- None.

### S1-DEVOPS-004: Logging & Test Scaffolds (Priority: Medium, Stream: DevOps & Tooling)
**Description**
Introduce Serilog logging baseline and initial test project.
**Status:** Completed

**Acceptance Criteria**
- Serilog configured with console sink and request logging.
- `Identity.Base.Tests` project created with xUnit + FluentAssertions references and sample test verifying `/healthz`.
- CI placeholder workflow documented (GitHub Actions or equivalent) describing build/test steps.

**Tasks**
- [x] Add Serilog packages; configure in `Program.cs` using `UseSerilog()` with JSON console output and enrichment for environment/service name.
- [x] Scaffold test project referencing API project; add `WebApplicationFactory` based smoke test.
- [x] Draft `.github/workflows/ci.yml` (or docs instructions) outlining build/test commands and caching strategy.

**Dependencies**
- S1-API-001.
