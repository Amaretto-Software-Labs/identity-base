# Database Design & Migration Guidelines
**Scope:** Platform API services (.NET 9 minimal APIs) using EF Core + PostgreSQL

---

## 1. Core Principles
- **Domain-first modeling:** start with explicit aggregates (Communities, Memberships, Incidents) and map them to EF Core entities with clear ownership and navigation properties.
- **Consistency over cleverness:** prefer explicit property definitions, avoid magic conventions, and document every schema decision next to the model.
- **Structured logging:** use Serilog enrichers for database-related events (migrations, transactions) to aid observability.
- **Immutable identifiers:** use UUID (`uuid`/`Guid`) primary keys for all tables; generate them application-side to avoid round trips.
- **Minimal API alignment:** keep DbContext registration and endpoint wiring inside the minimal API bootstrap (no MVC controllers) to simplify dependency graphs.
- **Automated migrations:** every API instance applies pending EF Core migrations during startup (via `Database.Migrate()`), eliminating manual SQL deployment drift.

---

## 2. Tools & Workflow
1. **Design:** capture ERD sketches (Mermaid/Draw.io) in `/apps/api/docs/erd/` before implementing.
2. **Model:** define EF Core entities and `DbContext` configuration in `/apps/api/src/Data` (use `IEntityTypeConfiguration` per aggregate).
3. **Migration:** generate migrations via EF Core CLI (`dotnet ef migrations add <Name> -p apps/api/src -s apps/api/src`). Store them in `/apps/api/src/Migrations`.
4. **Review:** migrations must be human-reviewed; ensure column types, default values, constraints, and index names are intentional.
5. **Apply:** the API automatically applies migrations on startup (`using var scope = app.Services.CreateScope(); scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();`). CLI `dotnet ef database update` is reserved for local troubleshooting only.
6. **Document:** update `/apps/api/README.md` with migration name, purpose, and rollback steps.

---

## 3. Unit of Work & DbContext Usage
- Wrap EF Core access behind a Unit of Work abstraction (`IUnitOfWork`) that coordinates repositories and transaction boundaries per request.
- Register the Unit of Work in dependency injection with scoped lifetime; expose `CommitAsync`/`RollbackAsync` semantics.
- Minimal API endpoints resolve Unit of Work instances instead of raw DbContext, promoting consistent transactional behavior and testability.
- Keep repositories thin; business logic lives in domain/application services that compose operations via the Unit of Work.

---

## 4. Schema Best Practices
- **Primary keys:** `Guid` with `ValueGeneratedNever()`; configure default `uuid_generate_v4()` in migrations for direct SQL usage.
- **Foreign keys:** cascade delete only where business rules allow; otherwise restrict and enforce via service layer.
- **Timestamps:** use `CreatedAt`/`UpdatedAt` columns with UTC `timestamptz`; set via EF Core interceptors.
- **Soft deletes:** prefer `IsDeleted` flag + filtered indexes when audit requirements demand retention.
- **Indexes:** create explicit indexes for common filters (CommunityId, Status, CreatedAt). Name them `IX_<Table>_<Columns>`.
- **Enums:** map to PostgreSQL enums via `HasConversion<string>()` or dedicated enum types; avoid magic integers.
- **Auditing:** maintain append-only audit tables for sensitive transitions; seed data via migrations sparingly (only configuration values).

---

## 5. Branching & Collaboration
- Each feature branch must include its own migration(s); do not reuse existing migration files.
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
- [ ] Verify startup auto-applies migrations in local environment.
- [ ] Write/extend tests covering new behavior.
- [ ] Update documentation and changelog entries.
- [ ] Verify minimal API endpoints compile and expose necessary contracts.

Following these conventions keeps our PostgreSQL schema aligned across services while leveraging EF Core migrations, the Unit of Work pattern, and .NET 9 minimal APIs safely.
