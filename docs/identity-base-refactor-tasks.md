# Identity.Base Modularization Task Breakdown

## Phase 1 – Structural Refactor (Highest Priority)
1. Confirm project names, namespaces, and NuGet IDs remain `Identity.Base`, `Identity.Base.AspNet`, and `Identity.Base.Host`.
2. Convert `Identity.Base` to `Microsoft.NET.Sdk` class library; remove web-specific assets (launch settings, Kestrel config) as needed.
3. Create `Identity.Base.Host` web project with minimal `Program` that composes `Identity.Base` and `Identity.Base.AspNet`.
4. Update `Identity.sln` to include the host app and adjust project references/test dependencies.
5. Move hosting-specific code from `Identity.Base/Program.cs` into the new host while keeping reusable services in the library.
6. Ensure DI registration and minimal API endpoint extension methods are exported from `Identity.Base` and consumed by the host.
7. Introduce design-time `DbContextFactory` for `AppDbContext` so EF tools work from the library.
8. Verify solution builds and existing tests run against the refactored structure.

## Phase 2 – API Hardening & Extensibility (High Priority)
1. Review public surface of `Identity.Base` and `Identity.Base.AspNet`; trim or encapsulate internal-only types.
2. Formalize fluent configuration API: `services.AddIdentityBase(options => { ... }).AddExternalAuthProvider(...)`, plus first-party helpers for Google, Microsoft, Apple.
3. Abstract configuration sourcing to allow additional providers (e.g., database-backed settings) while retaining JSON defaults.
4. Validate options binding and `IValidateOptions` coverage in the new library context; add integration tests for failure cases.
5. Ensure external auth, MailJet, MFA, and other integrations can be swapped via DI and documented extension points.
6. Re-run migrations to ensure they function through the design-time factory; document usage for consumers.
7. Add or adapt unit/integration tests exercising the host and library APIs separately.

## Phase 3 – Documentation, Packaging, and Release (Medium Priority)
1. Update `README.md`, `docs/getting-started.md`, and new plan docs to reflect NuGet-based consumption.
2. Produce example configuration files and explain how to override providers/configuration sources.
3. Configure CI/CD to build, test, and pack `Identity.Base` and `Identity.Base.AspNet`; add symbol/source link settings.
4. Draft release notes and migration guidance in `CHANGELOG.md` for package consumers.
5. Publish prerelease packages (alpha) and validate with a sample consumer app in `apps/`.
6. Collect feedback, triage issues, and plan follow-up stories for GA readiness.

## Ongoing / Cross-Phase Considerations (Parallel Tasks)
- Keep stakeholders updated through sprint backlog and project plan documents.
- Coordinate with security/reliability reviewers before publishing packages.
- Monitor for regression risks in existing integrations during refactor and packaging.
