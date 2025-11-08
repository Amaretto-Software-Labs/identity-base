# Identity Base Provider-Agnostic Plan

## Goal
Transform Identity.Base into a database-provider-agnostic library bundle where consuming hosts:
- Configure every DbContext (Identity, Roles, Organizations, sample apps) directly with their preferred EF Core provider (PostgreSQL, SQL Server, etc.).
- Generate and own migrations in their host projects (we no longer ship canonical migrations or run them automatically).
- Opt into our seeders and hosted services only after applying their migrations.

## Guiding Principles
- **No built-in migrations**: delete existing migration histories and migration-hosted services; hosts run `dotnet ef migrations` themselves.
- **Single configuration hook**: accept optional delegates (e.g., `Action<IServiceProvider, DbContextOptionsBuilder>`) on our registration methods so hosts can supply provider-specific configuration once and reuse it across contexts.
- **Provider neutrality**: eliminate Npgsql-only annotations and behaviors; rely on EF Core primitives and let providers plug in via options.
- **Explicit host responsibilities**: documentation must highlight the need to add DbContexts, generate migrations, apply them, then run our builders/seeders.
- **Parity validation**: maintain automated verification across at least PostgreSQL and SQL Server to ensure our code paths remain compatible.

## Workstreams
1. **Remove repo-owned migrations**
   - Delete migrations across Identity.Base, Identity.Base.Roles, Identity.Base.Organizations, and sample hosts/tests.
   - Remove migration resources from project files and NuGet packaging.
   - Update tests/CI to stop depending on embedded migrations.

2. **Eliminate migration hosted services**
   - Remove `MigrationHostedService`, `IdentityRolesMigrationHostedService`, `IdentityRolesSeedHostedService`, `OrganizationMigrationHostedService`, and any other components that call `Database.Migrate()` automatically.
   - Update seeders (Identity data, roles, OpenIddict, organizations) to validate prerequisites and clearly instruct hosts to run migrations first.

3. **Rework DbContext registration**
   - Delete `DatabaseOptions` and any automatic connection-string binding logic.
   - Update `AddIdentityBase`, `AddIdentityAdmin`, `AddIdentityRoles`, `AddIdentityOrganizations`, sample host apps, and tests to accept optional `configureDbContext` delegates that we invoke for each internal context.
   - Ensure builder methods throw meaningful exceptions if required DbContexts were not registered/configured.

4. **Provider compatibility sweep**
   - Audit entity configuration for provider-specific annotations (identity columns, enums, PostgreSQL types) and replace with provider-neutral EF APIs.
   - Review raw SQL, migrations-related utilities, and options to confirm they work under `UseSqlServer` as well as `UseNpgsql`.
   - Provide extensibility hooks for provider-specific tuning (retry policies, execution strategies) through the configuration delegate.

5. **Testing and validation**
   - Update integration tests to spin up containerized PostgreSQL and SQL Server instances, scaffold transient migrations at test time, and execute core workflows.
   - Maintain fast in-memory tests where possible, but ensure seeding/end-to-end flows are exercised on real providers.
   - Adjust CI workflows to run the dual-provider suite.

6. **Documentation & samples**
   - Refresh `README.md`, getting-started guides, admin/operations docs, and sprint notes to describe the new workflow for configuring DbContexts and generating migrations.
   - Provide copy-paste samples for both PostgreSQL and SQL Server (connection strings, `AddDbContext` snippets, `dotnet ef` commands).
   - Highlight breaking changes and upgrade steps in `CHANGELOG.md`.

## Execution Phases
1. **Foundation Cleanup**
   - Remove migrations and migration services.
   - Introduce the new `configureDbContext` delegate pattern and update sample hosts/tests accordingly.
2. **Provider Neutrality & Tests**
   - Complete the provider compatibility sweep.
   - Add the dual-provider integration tests and CI hooks.
3. **Docs & Release Prep**
   - Update documentation, changelog, and release notes.
   - Communicate breaking changes and publish preview packages for validation.

## Risks & Mitigations
- **Consumer migration drift**: mitigate by providing detailed guidance and sample migrations/scripts.
- **Hidden provider-specific assumptions**: catch via dual-provider automated tests and manual review of entity configurations.
- **Adopter friction**: offer turnkey delegate examples and optional helper extensions to keep setup minimal despite the added flexibility.

## Next Steps
1. Confirm delegate signature(s) and whether we need per-context overrides.
2. Create tracking issues/tasks per workstream in the current sprint backlog.
3. Begin Foundation Cleanup once the plan is approved.
