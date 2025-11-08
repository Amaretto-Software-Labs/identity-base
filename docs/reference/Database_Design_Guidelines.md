# Database Design & Migration Guidelines
**Scope:** Platform API services (.NET 9 minimal APIs) using EF Core with PostgreSQL or SQL Server (PostgreSQL is still the default reference stack).

---

## 1. Core Principles
- **Domain-first modeling:** start with explicit aggregates and map them to EF Core entities with clear ownership and navigation properties.
- **Consistency over cleverness:** prefer explicit property definitions, avoid magic conventions, and document every schema decision next to the model.
- **Structured logging:** use Serilog enrichers for database-related events (migrations, transactions) to aid observability.
- **Immutable identifiers:** use UUID (`uuid`/`Guid`) primary keys for all tables; generate them application-side to avoid round trips.
- **Minimal API alignment:** keep DbContext registration and endpoint wiring inside the minimal API bootstrap (no MVC controllers) to simplify dependency graphs.
- **Host-owned migrations:** each consuming host configures the DbContexts, generates EF Core migrations targeting its chosen provider, and applies them (via `dotnet ef database update` or an explicit startup hook) *before* Identity Base seeders execute.

---

## 2. Tools & Workflow
1. **Design:** capture ERD sketches (Mermaid/Draw.io) in `Identity.Base/docs/erd/` before implementing.
2. **Model:** define EF Core entities and `DbContext` configuration in the package source (e.g., `Identity.Base/Data`) using `IEntityTypeConfiguration` per aggregate.
3. **Migration:** generate migrations from the consuming host project (for example `Identity.Base.Host`, sample APIs, or your product host) so they compile against your provider/configuration. Store them under the host (e.g., `Identity.Base.Host/Data/Migrations/<Context>`), not inside the shared packages.
4. **Review:** migrations must be human-reviewed; ensure column types, default values, constraints, and index names are intentional.
5. **Apply:** run migrations via `dotnet ef database update` (CI/deploy pipeline) or an explicit startup helper in the host (`using var scope = app.Services.CreateScope(); await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();`). Identity Base does not auto-apply them for you.
6. **Document:** update `docs/` and release notes with the migration name, purpose, and rollback steps so other hosts know when to regenerate.

---

## 3. Unit of Work & DbContext Usage
- Wrap EF Core access behind a Unit of Work abstraction (`IUnitOfWork`) that coordinates repositories and transaction boundaries per request.
- Register the Unit of Work in dependency injection with scoped lifetime; expose `CommitAsync`/`RollbackAsync` semantics.
- Minimal API endpoints resolve Unit of Work instances instead of raw DbContext, promoting consistent transactional behavior and testability.
- Keep repositories thin; business logic lives in domain/application services that compose operations via the Unit of Work.

---

## 4. Schema Best Practices
- **Table naming:** ensure tables remain PascalCase (e.g., `UserProfile`). Use the `UseTablePrefix`/`IdentityDbNamingOptions` helpers so hosts can override the default `Identity_` prefix.
- **Primary keys:** `Guid` with `ValueGeneratedNever()`; configure default `uuid_generate_v4()` in migrations for direct SQL usage.
- **Foreign keys:** cascade delete only where business rules allow; otherwise restrict and enforce via service layer.
- **Timestamps:** use `CreatedAt`/`UpdatedAt` columns with UTC `timestamptz`; set via EF Core interceptors.
- **Soft deletes:** prefer `IsDeleted` flag + filtered indexes when audit requirements demand retention.
- **Indexes:** create explicit indexes for common filters (CommunityId, Status, CreatedAt). Name them `IX_<Table>_<Columns>`.
- **Enums:** map to PostgreSQL enums via `HasConversion<string>()` or dedicated enum types; avoid magic integers.
- **Auditing:** maintain append-only audit tables for sensitive transitions; seed data via migrations sparingly (only configuration values).

---

## 5. Branching & Collaboration
- Each feature branch in a host must include its own migration(s); do not reuse existing migration files.
- Resolve migration conflicts by reordering or regenerating on top of `main`; never edit past migrations already applied to shared environments.
- Add regression tests (unit/integration) verifying new constraints or relationships before merging.

---

## 6. Minimal API Integration
- Register `DbContext`/Unit of Work and repositories inside the minimal API builder (`var builder = WebApplication.CreateBuilder(...)`).
- Group endpoints using `MapGroup` and inject Unit of Work via minimal API parameter binding.
- Keep DTOs and validators close to endpoints; reuse shared validation packages (`/packages/validation`).

---

## 7. Engineering Best Practices
- Follow SOLID, clean architecture, and enterprise-grade coding standards; keep methods and classes short, single-purpose, and self-documenting.
- Prefer expressive naming over comments; add summaries only when intent is non-obvious.
- Ensure every schema change ships with automated tests (unit/integration) and clear rollback guidance.
- Adhere to the shared [Engineering Principles](Engineering_Principles.md) for additional guidance covering backend and frontend code quality.

---

## 8. Checklist (per change)
- [ ] Update ERD or data flow notes.
- [ ] Implement EF Core entity/config changes (respect Unit of Work boundaries).
- [ ] Create migration and inspect generated SQL.
- [ ] Verify your host applied the migrations locally (either via CLI output or startup logs).
- [ ] Write/extend tests covering new behavior.
- [ ] Update documentation and changelog entries.
- [ ] Verify minimal API endpoints compile and expose necessary contracts.

Following these conventions keeps schemas aligned across services while letting each host own its EF Core migrations, the Unit of Work pattern, and database provider choices safely.
