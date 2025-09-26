# Identity.Base Modularization Plan

## Goal
Deliver Identity.Base as a reusable NuGet library that encapsulates authentication, user management, MFA, and OpenIddict integration while providing:
- `Identity.Base` class library for core services, EF Core context, options, seeding, and feature endpoints.
- `Identity.Base.AspNet` integration library with opinionated pipeline/DI helpers.
- `Identity.Base.Host` ASP.NET Core application acting as the reference host implementation.

## Target Architecture
- **Identity.Base (library)**
  - Contains domain models, EF Core context/migrations (`Data`, `Identity`, `OpenIddict`, `Options`, `Seeders`).
  - Provides feature services, validators, and endpoint registration helpers (`Features`, `Extensions`).
  - No direct dependency on `WebApplication`; expose extensions on `IServiceCollection` and `IEndpointRouteBuilder`.
- **Identity.Base.AspNet (library)**
  - Offers streamlined ASP.NET Core wiring (JWT auth, middleware hooks, developer SSL helpers).
  - Re-exports `MapIdentityEndpoints` and similar helpers for consumers referencing only this package.
- **Identity.Base.Host (app)**
  - Slim `net9.0` web app hosting the libraries.
  - Supplies configuration sources, logging (Serilog), storage providers, and production-ready defaults.
  - Serves as runnable sample and regression host for tests.

## Migration Plan
1. **Project Setup**
   - Change `Identity.Base` project SDK to `Microsoft.NET.Sdk`; adjust output to class library.
   - Add new `Identity.Base.Host` web project with minimal `Program` that composes the packages.
   - Update `Identity.sln` to include the host and preserve test project references.
2. **Code Relocation**
   - Move all non-host code from `Identity.Base/Program.cs` and supporting folders into library-friendly structures.
   - Replace direct usage of `WebApplicationBuilder` with DI/service abstractions and expose minimal API registration methods.
   - Keep endpoint classes but ensure they can run inside arbitrary hosts.
3. **Configuration Surface**
   - Preserve option classes (`DatabaseOptions`, `RegistrationOptions`, `MfaOptions`, etc.) and ensure validation occurs via DI.
   - Provide sample `appsettings.json` in host; document required configuration sections for consumers.
   - Allow hosts to override or supplement configuration providers (e.g., database-backed settings) while keeping library defaults.
   - Allow hosts to override email/MFA providers through interfaces.
4. **Database & Migrations**
   - Keep migrations inside `Identity.Base` and add a design-time factory for `dotnet ef` usage.
   - Ensure seeding hosted services remain optional and can be triggered by host.
5. **External Integrations**
   - Maintain MailJet implementation as default, but expose abstractions for custom providers.
   - Expose fluent extension points so hosts opt into providers (`services.AddIdentityBase(...).AddGoogleAuth().AddMicrosoftAuth().AddAppleAuth()`).
   - Provide a generic hook (`AddExternalAuthProvider`) so additional providers can plug in without modifying the package.
   - Verify external auth configuration (Google, Microsoft, Apple, etc.) reads from host-supplied options and supports add/remove at composition time.
6. **Tests & Samples**
   - Retarget `Identity.Base.Tests` to build against the library and exercise the host via `WebApplicationFactory` as needed.
   - Add (or update) sample apps under `apps/` that reference the NuGet packages for integration testing.

## Build & Packaging
- Update project metadata for `Identity.Base` and `Identity.Base.AspNet` (authors, license, README, release notes).
- Configure CI to restore, build, run tests, pack both libraries, and optionally publish prerelease packages.
- Provide symbol packages if desired; ensure deterministic builds and SourceLink are enabled.

## Documentation Updates
- Revise `README.md` and `docs/getting-started.md` with NuGet usage, configuration requirements, and hosting instructions.
- Add migration guidance to `CHANGELOG.md` for downstream consumers.
- Document configuration schemas and option classes in `docs/` as needed.

## Execution Phases
1. **Phase 1 – Structural Refactor**
   - Introduce new project skeletons, move code, update namespaces, and restore builds/tests.
2. **Phase 2 – API Hardening & Tooling**
   - Trim public surface, finalize options, ensure migrations and tests run from package consumers, refine logging/extensibility.
3. **Phase 3 – Documentation & Release**
   - Refresh documentation, update CI/CD pipelines, publish alpha NuGet packages, and gather consumer feedback.

## Immediate Next Steps
- Confirm naming, versioning, and compatibility expectations with stakeholders.
- Create tracking issues per phase/story in the current sprint backlog.
- Open a feature branch and begin Phase 1 once the plan is approved.
