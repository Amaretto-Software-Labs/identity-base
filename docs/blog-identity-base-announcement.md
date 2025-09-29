# Announcing Identity Base: Modern Identity for .NET 9 Teams

Identity Base is now available as an open-source, modular Identity + OpenID Connect platform for .NET 9. Built by Amaretto Software Labs and supported by VA Software Solutions, it packages the pieces teams expect — ASP.NET Core Identity, Entity Framework Core migrations, OpenIddict server setup, multi-factor authentication, external sign-in providers, MailJet-powered communications, and optional RBAC/admin extensions — into a cohesive solution you can self-host or embed. Explore the source on the [Identity Base GitHub repository](https://github.com/Amaretto-Software-Labs/identity-base).

The project stands on the shoulders of the phenomenal [OpenIddict](https://github.com/openiddict/openiddict-core) community. Their OpenID Connect server gives Identity Base its protocol core, and this release simply wouldn’t be possible without that open-source foundation.

## What is Identity Base?
Identity Base stitches together the identity building blocks you already rely on and bundles them into NuGet packages ([`Identity.Base`](https://www.nuget.org/packages/Identity.Base), [`Identity.Base.AspNet`](https://www.nuget.org/packages/Identity.Base.AspNet), [`Identity.Base.Roles`](https://www.nuget.org/packages/Identity.Base.Roles), [`Identity.Base.Admin`](https://www.nuget.org/packages/Identity.Base.Admin)) and a runnable host. It applies secure defaults, enforces configuration validation, and keeps the entire stack under the permissive MIT License. Whether you run the included host or drop the services into an existing application, you get a consistent, supportable authentication layer.

## What You Get Out of the Box
- Identity and OpenIddict orchestration with password, authorization-code PKCE, refresh tokens, seeded clients, and scope configuration.
- Multi-factor authentication with authenticator apps, SMS, email challenges, and recovery codes.
- External provider integrations for Google, Microsoft, and Apple, plus fluent extension points for other providers.
- MailJet email workflows for confirmation, reset, MFA challenges, and error reporting baked into the pipeline.
- An extensible dependency-injection surface that lets you plug in your own option validators, templated email senders, challenge dispatchers, audit logging, and return URL validators.
- Secure-by-default behaviors such as password grant gating, normalized return URLs, redacted request logging, and dedicated health checks.

## Where Identity Base Fits
- Launching a self-managed identity server: run the `Identity.Base.Host` project, apply the shipped EF Core migrations, and serve browsers, SPAs, and native apps with an OpenIddict authority.
- Embedding identity quickly: reference the `Identity.Base` package inside your own host to expose the same pipeline without cloning the repository.
- Protecting existing APIs: consume Identity Base-issued tokens with the `Identity.Base.AspNet` helpers, enabling JWT bearer authentication and scope-driven authorization in minutes.
- Delivering multi-channel user journeys: configure MailJet templates, external sign-in providers, and MFA policies through `appsettings` so that onboarding flows stay flexible without code changes.

## Extend with Roles and Admin APIs
Identity Base now ships dedicated packages for role-based access control and privileged administration:

- [`Identity.Base.Roles`](https://www.nuget.org/packages/Identity.Base.Roles) adds EF Core-backed roles, permissions, assignments, and seeding plus user-permission endpoints that downstream applications can query.
- [`Identity.Base.Admin`](https://www.nuget.org/packages/Identity.Base.Admin) layers on protected admin APIs (`/admin/users`, `/admin/roles`), authorization helpers, and integrations with the roles package for auditing and role assignment.

Opt in by referencing the packages you need, registering the provided services, and (optionally) using the `IdentityRolesDbContext` before seeding `SeedIdentityRolesAsync()` at startup. Configuration walkthroughs live in the [Identity.Base.Roles README](https://github.com/Amaretto-Software-Labs/identity-base/blob/main/Identity.Base.Roles/README.md) and [Identity.Base.Admin README](https://github.com/Amaretto-Software-Labs/identity-base/blob/main/Identity.Base.Admin/README.md).

## Getting Started in Minutes
Identity Base expects the .NET 9 SDK, PostgreSQL 16, and MailJet credentials. Clone the repository, restore dependencies, configure `appsettings.Development.json`, and run:

```bash
dotnet restore Identity.sln
dotnet build Identity.sln
dotnet ef database update \
  --project Identity.Base/Identity.Base.csproj \
  --startup-project Identity.Base.Host/Identity.Base.Host.csproj
dotnet run --project Identity.Base.Host/Identity.Base.Host.csproj
```

Configuration sections such as `Registration`, `MailJet`, `Mfa`, `ExternalProviders`, `OpenIddict`, and `Cors` control metadata, templates, second-factor options, provider secrets, and client scopes. The [Getting Started guide](https://github.com/Amaretto-Software-Labs/identity-base/blob/main/docs/guides/getting-started.md) walks through each setting, including optional admin seeding and Twilio integration for SMS.

## Bring Your APIs Along
When you already have downstream services, `Identity.Base.AspNet` keeps your code minimal. Add the package, point it at your identity host, and wire authorization:

```csharp
using Identity.Base.AspNet;

builder.Services.AddIdentityBaseAuthentication("https://identity.yourdomain.com");

var app = builder.Build();
app.UseIdentityBaseRequestLogging();
app.UseIdentityBaseAuthentication();

app.MapGet("/api/protected", () => "Secure content")
   .RequireAuthorization(policy => policy.RequireScope("identity.api"));
```

This extension handles JWT bearer authentication, request logging, and scope assertions so your APIs can focus on business logic.

## Built for Real-World Operations
Identity Base bakes in observability and guardrails: correlation-aware Serilog logging, audit hooks for MFA and profile changes, granular health checks for the database, MailJet configuration, and provider readiness. The GitHub Actions CI workflow builds, tests (44 integration tests), and packages every change, and the release checklist codifies how to produce signed NuGet packages.

## Explore the Samples and Docs
A React sample client ([`apps/sample-client`](https://github.com/Amaretto-Software-Labs/identity-base/tree/main/apps/sample-client)) demonstrates registration, MFA, external link flows, and an admin console powered by the Identity Base Admin endpoints. A minimal ASP.NET Core API ([`apps/sample-api`](https://github.com/Amaretto-Software-Labs/identity-base/tree/main/apps/sample-api)) shows how to consume Identity Base tokens. Dive into the [project README](https://github.com/Amaretto-Software-Labs/identity-base/blob/main/README.md) for a feature tour, browse the [docs directory](https://github.com/Amaretto-Software-Labs/identity-base/tree/main/docs) for deeper guides, and open [GitHub issues](https://github.com/Amaretto-Software-Labs/identity-base/issues) when you have questions or requests. For security or conduct-related concerns, follow the guidance in the [support section](https://github.com/Amaretto-Software-Labs/identity-base#support).

Identity Base is ready for teams who want enterprise-grade identity without months of assembly work. Grab the [source code](https://github.com/Amaretto-Software-Labs/identity-base), configure your credentials, review the [contribution guidelines](https://github.com/Amaretto-Software-Labs/identity-base/blob/main/CONTRIBUTING.md), and start shipping secure sign-in experiences today.
