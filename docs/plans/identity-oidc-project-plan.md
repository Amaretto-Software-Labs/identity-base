# Identity & OIDC Project Plan

## Objectives
- Deliver a .NET 9 minimal API that issues OpenID Connect tokens via OpenIddict, backed by ASP.NET Core Identity and PostgreSQL.
- Support consumption as a drop-in identity package for other services while retaining a clear path to evolve into a standalone cloud identity platform.
- Expose minimal API endpoints for registration, login, email confirmation, password resets, and token-protected profile access.
- Keep OpenIddict client, scope, and server settings configuration-driven so environments can be managed without code changes.
- Align with Amaretto Software Labs engineering principles, database guidelines, and the MailJet email sender playbook.
- Ship with end-to-end implementation docs, integration guides, and open-source readiness (licence, contribution guide) so teams can adopt it quickly.
- Provide a first-class Docker experience so the service can run self-hosted without additional packaging.
- Deliver secure email/password authentication with optional MFA (TOTP, recovery codes) and pluggable social sign-in providers (Google, Apple, Microsoft, etc.).
- Allow integrators to capture rich user metadata (name, company, position, etc.) through configurable registration schemas without code changes.

## Guiding Principles & Conventions
- Follow minimal API architecture: organise endpoints into feature-based groups, keep `Program.cs` focused on bootstrap, and delegate setup to extension methods/modules per feature (`Services`, `Endpoints`, `Pipeline`).
- Enforce the Unit of Work pattern: endpoint handlers depend on coordinators/repositories rather than raw `DbContext` access.
- Centralise error handling with ProblemDetails responses and named authorization policies driven by `ICurrentUserService`.
- Keep classes/methods small (≤200/40 lines) and colocate request/response contracts with their feature folders.
- Record ERD sketches under `Identity.Base/docs/erd/` before introducing entities; update documentation with every schema change.
- Tests accompany features (unit + integration/service tests). Mock only external boundaries such as MailJet or external identity providers.

## Solution Structure
```
Identity.sln
/Identity.Base
  Program.cs
  /Extensions (service + pipeline wiring)
  /Features
    /Authentication
    /Users
    /Email
  /Data (DbContext, configurations, Unit of Work, repositories)
  /Options (configuration objects, validation)
  appsettings.json
  appsettings.Development.json
  /docs (service-specific onboarding, ERDs)
/docs
  identity-oidc-project-plan.md
  Engineering_Principles.md
  Database_Design_Guidelines.md
  mailjet-email-sender.md
```
- Optionally add `/apps/shared` for reusable contracts if/when other services need them; keep this plan within a single API project until reuse demands separation.

## Deployment & Integration Modes
- `Source drop-in`: deliver the codebase as a ready-made folder (`Identity.Base`) that can be copied into an existing solution; expose configuration-first extensibility (custom user fields, policies, event hooks) via partial classes and options-based registration so teams can tune behaviour without forking.
- `Docker self-hosted`: ship a Dockerfile and compose samples that run the service alongside PostgreSQL and optional MailJet stubs; document required environment variables, exposed ports, and health checks for platform onboarding.
- Keep secrets and client registrations in configuration so both modes align with environment provisioning pipelines and Infrastructure-as-Code templates.
- Provide extension points (`IIdentityEventHandler`, webhook emitters, or message bus integration) so downstream services can react to identity lifecycle events without diverging from upstream; include hooks for MFA and external login lifecycle events.
- Maintain automated smoke tests covering both integration paths (drop-in + container) to ensure behavioural parity as the platform evolves.

## Documentation & Open Source Readiness
- Create `/docs/guides/getting-started.md` with copy-paste steps for dropping the solution into an existing repo (folder structure, configuration wiring, migration execution, MailJet setup).
- Author `/docs/guides/integration-guide.md` that walks consuming apps through registering clients/scopes, wiring minimal API endpoints, invoking token flows, configuring 2FA, enabling external providers, and extending user metadata; include Postman collections.
- Provide `/docs/guides/docker.md` describing container configuration, sample `docker-compose` with PostgreSQL, environment variable matrix for all settings, and guidance for injecting provider secrets securely.
- Add open-source artefacts: `LICENSE`, `CONTRIBUTING.md`, and issue/PR templates aligned with project standards.
- Keep change logs (`CHANGELOG.md`) and migration docs current so adopters can track upgrades.

## Core Packages & Tooling
- Identity + EF Core: `Microsoft.AspNetCore.Identity.EntityFrameworkCore`, `Microsoft.EntityFrameworkCore`, `Npgsql.EntityFrameworkCore.PostgreSQL`.
- OpenIddict: `OpenIddict.Server.AspNetCore`, `OpenIddict.Validation.AspNetCore`, `OpenIddict.EntityFrameworkCore`.
- Minimal APIs support: `Microsoft.AspNetCore.Authentication.JwtBearer`, `Microsoft.Extensions.Options.ConfigurationExtensions`.
- Email: `Mailjet.Api` (3.0.0) with `Microsoft.AspNetCore.Identity.UI.Services`.
- Observability/Logging: `Serilog.AspNetCore`, `Serilog.Sinks.Console`, enrichers for EF migrations.
- Testing: `xUnit`, `Shouldly`, `Microsoft.AspNetCore.Mvc.Testing`, `Testcontainers.PostgreSql`.
- Sample harness: Vite, React 18, React Router, TailwindCSS, Axios/Fetch wrappers, Cypress or Playwright for E2E.
- External providers: `Microsoft.AspNetCore.Authentication.Google`, `.MicrosoftAccount`, `.OpenIdConnect` (Apple), plus provider-specific SDKs as required.

## Configuration & Secrets
- `appsettings.json` holds sections:
  - `ConnectionStrings:Primary` (PostgreSQL).
  - `Identity:Password` policy settings.
  - `OpenIddict:Server` (endpoints, token lifetimes) and `OpenIddict:Applications` array (client id/secret, permissions, redirect URIs, grant types).
  - `OpenIddict:Scopes` (API scopes to seed).
- `MailJet` section per MailJet guide (API keys, sender, template IDs).
- Use environment-specific overrides (`appsettings.Development.json`, secrets.json, environment variables).
- Provide POCO options + validation for each section; register with `OptionsBuilder.ValidateOnStart()`.
- Surface configuration binding through extension methods so host applications can override policies, claim mappers, or storage options without modifying core code.
- Support configurable user metadata fields by binding a `Registration:ProfileFields` section that drives validation, persistence, and claim projection.

## Database & Persistence Strategy
- Implement `AppDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>`; include OpenIddict entity sets via `builder.UseOpenIddict()`.
- Configure EF Core with `UseNpgsql` and retain PascalCase table names. If a prefix improves clarity, apply `Identity_` (e.g., `Identity_UserProfile`). Set `AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", false)` if required.
- Follow database guidelines:
  - All primary keys are `Guid` with `ValueGeneratedNever()`; migrations default to `uuid_generate_v4()`.
  - Add `CreatedAt/UpdatedAt` via interceptors; consider `IsDeleted` for soft deletes if requirements emerge.
- Keep entity configurations in `Identity.Base/Data/Configurations` implementing `IEntityTypeConfiguration<>`.
- Create `UnitOfWork` abstraction coordinating repositories; add transactional helpers for multi-step operations (registration + email send).
- Store extended user profile metadata using a strongly-typed value object backed by JSONB column(s) so integrators can add arbitrary fields (e.g., company, position) without schema churn; expose typed accessors and claim mapping helpers.
- Auto-apply migrations on startup (`Database.Migrate()` inside scoped service). Ensure migrations live under `Identity.Base/Migrations` and are reviewed before merge.
- Document each migration in `Identity.Base/docs/README.md` per checklist.

## Identity & User Management
- Require confirmed email before sign-in (`SignInOptions.RequireConfirmedEmail = true`).
- Configure password, lockout, and user options through `builder.Services.Configure<IdentityOptions>(...)`.
- Add token providers for email confirmation, password reset, and authenticator-based TOTP codes (e.g., `TotpSecurityStampBasedTokenProvider`).
- Extend `ApplicationUser` with profile fields (DisplayName, CreatedAt, etc.) aligned with domain modelling.
- Provide `IUserClaimsPrincipalFactory<ApplicationUser>` customization if scopes require extra claims.
- Enable multi-factor authentication using TOTP authenticator apps, backup codes, and optional email/SMS second factors; expose endpoints/flows to enroll, verify, and reset MFA devices.
- Integrate external login providers (Google, Apple, Microsoft, etc.) via `AddAuthentication().AddOpenIdConnect()`/`AddOAuth()`, storing provider keys and linking them to local users for hybrid identities.
- Surface per-tenant/provider configuration in `appsettings` (client IDs/secrets, callback URIs, scopes) with options validation and secure secret loading for production.
- Expose extensible user profile metadata with validation rules per field (required, max length, regex) so integrators can capture name, company, position, and additional attributes during registration; project metadata into tokens/claims when configured.

## OpenIddict Configuration
- Register OpenIddict server/components via extension method `AddIdentityOidc(builder.Configuration)` that:
  - Enables endpoints (`/connect/token`, `/connect/authorize`, `/connect/userinfo`, `/connect/introspect`, `/connect/logout`).
  - Supports authorization code + PKCE, refresh token, client credentials (optional) per configuration.
  - Uses ASP.NET Core Data Protection and integrates with Identity for password/refresh flows.
  - Issues JWT access tokens and optionally reference tokens; store authorizations and tokens in EF entities.
- Implement configuration-driven seeding:
  - Define options binding for `OpenIddict:Applications` and `OpenIddict:Scopes`.
  - Add hosted service that runs at startup to upsert clients/scopes based on configuration, hashing secrets with `OpenIddictConstants.ClientTypes`. Ensure idempotence.
- Configure validation stack for resource APIs (self-contained tokens or introspection).

## Minimal API Endpoints
- Group endpoints under `/auth` and `/users` with `MapGroup`:
  - `POST /auth/register` – create user, send confirmation email.
  - `POST /auth/login` – issue token via OpenIddict password flow or redirect to `/connect/token`.
  - `POST /auth/confirm-email` – verify confirmation token.
  - `POST /auth/resend-confirmation` – regenerate confirmation email.
  - `POST /auth/forgot-password` + `POST /auth/reset-password` – issue/reset via Identity tokens.
  - `GET /users/me` – protected profile endpoint requiring `RequireAuthorization("User.Read")`.
- MFA endpoints: `POST /auth/mfa/enroll`, `POST /auth/mfa/verify`, `POST /auth/mfa/disable`, `POST /auth/mfa/recovery-codes`.
- External login endpoints: begin/complete OAuth flows, link/unlink providers (`/auth/external/{provider}/start`, `/auth/external/{provider}/callback`, `/auth/external/{provider}/unlink`).
- Metadata endpoints: `GET /auth/profile-schema` to expose configurable fields, `PUT /users/me/profile` to update metadata with server-side validation and audit logging.
- Handlers orchestrate through dedicated command/services (e.g., `RegisterUserCommandHandler`) to keep endpoints thin.
- Apply FluentValidation for request DTOs; leverage `Results<Ok, ValidationProblem>` responses with consistent ProblemDetails.

## Email Delivery (MailJet)
- Implement `MailJetEmailSender` per guide in `Identity.Base/Features/Email`:
  - Bind `MailJetOptions` from configuration and enforce template-only sends.
  - Support template variables for confirmation/reset flows (e.g., `verificationLink`, `displayName`).
- Register as both `IEmailSender` and a custom `ITemplatedEmailSender`; injection into registration/password services.
- Configure template IDs for confirmation/password flows in every environment and ensure error reporting inbox is monitored.
- Add integration tests that stub MailJet using test doubles to verify payload formation.

## Observability & Telemetry
- Configure Serilog early in `Program.cs` using structured logging, including EF migration logs and authentication events.
- Emit audit logs for security-sensitive actions (registration, login failures, token refreshes, MFA enroll/verify, external provider link/unlink).
- Integrate health checks (`/health`) covering database connectivity and background workers.

## Test Harness & Sample Client
- Build `Identity.SampleClient` as a Vite + React + Tailwind web app that demonstrates OIDC flows (register, login, refresh, password reset) against the API.
- Implement environment-driven configuration for issuer URL, client ID, and redirect URIs so the harness can target local Docker or drop-in deployments.
- Include reusable hooks/services for PKCE authorization code flow and token storage to help adopters bootstrap their own clients.
- Add coverage for MFA enrollment/verification and external provider sign-in; include device fallback UX.
- Add Cypress or Playwright smoke tests that run against the sample to verify end-to-end auth scenarios (password + MFA + social) during CI.
- Document setup steps in the integration guide, including how to register the harness as a client via appsettings configuration.
- Showcase metadata capture/editing in the registration/profile forms with dynamic field rendering driven by the `/auth/profile-schema` endpoint.

## Testing Strategy
- Unit tests: command/handler logic, token generation workflows.
- Integration tests: use `WebApplicationFactory` + Testcontainers PostgreSQL to cover register/login/token endpoints end-to-end, applying migrations in test setup.
- Security tests: validate password policy, email confirmation enforcement, and OIDC flows (authorization code with PKCE, refresh tokens).
- Frontend harness tests: run Cypress/Playwright suites against the React/Tailwind sample to verify major auth journeys and regression-test integration guides.
- MFA & external provider tests: cover enrollment, recovery code rotation, authenticator invalidation, and Google/Apple/Microsoft login flows (stubbed in CI, live-configurable for staging).
- Metadata tests: validate custom field configuration binding, server-side validation, claim projection, and UI rendering for dynamic profile fields.

## Delivery Roadmap
1. **Bootstrap** – Scaffold solution, configure Serilog, set up DI extension modules, ensure baseline minimal API compiles.
2. **Persistence Layer** – Implement `AppDbContext`, Unit of Work, repositories, and initial migration aligned with ERD documentation.
3. **Identity Foundation** – Configure ASP.NET Identity, password policies, email confirmation requirement, MailJet sender integration.
4. **OpenIddict Integration** – Add OpenIddict server/validation, bind configuration, implement seeding of clients/scopes, wire `/connect/*` endpoints.
5. **Feature Endpoints** – Build registration/login/confirmation/password workflows plus MFA enrollment/verification and external provider linkage with DTO validation and ProblemDetails responses.
6. **Security Enhancements** – Finalise MFA device management, recovery code rotation, provider secret handling, and audit logging for auth events.
7. **Dockerization & Deployment Assets** – Produce Dockerfile, `docker-compose` samples, environment variable documentation, and health probes for self-hosted deployments.
8. **Sample Harness & Integration Docs** – Build the React/Tailwind test harness, author implementation guides, and ensure copy-paste adoption steps are validated.
9. **Testing & Hardening** – Add unit/integration tests, exercise flows against Testcontainers PostgreSQL, validate logging/telemetry, and run smoke suites against both drop-in and container deployments (including harness E2E tests).
10. **Docs & Handover** – Update README, ERD assets, change logs, and prepare release notes plus open-source artefacts.

## Checklist Before Release
- [ ] All migrations generated, reviewed, documented, and auto-applied at startup.
- [ ] OpenIddict clients/scopes verified from configuration across environments.
- [ ] MailJet templates configured and sending via templated workflow only.
- [ ] Docker image published (or ready for self-hosting) with compose sample validated end-to-end.
- [ ] React/Tailwind harness registered as a client and passing smoke/E2E checks.
- [ ] MFA enrollment, verification, and recovery code flows validated (API + harness).
- [ ] External identity providers (Google, Apple, Microsoft) configured with test credentials and passing integration checks.
- [ ] Custom metadata fields configured, persisted, surfaced in tokens, and covered by automated tests.
- [ ] Minimal API endpoints covered by integration tests and security validation.
- [ ] Logging, health checks, and telemetry dashboards verified.
- [ ] Documentation (this plan, data model notes, README) updated and linked in project wiki.
