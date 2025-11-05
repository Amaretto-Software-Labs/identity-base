# Identity.Base

## Overview
`Identity.Base` is the core identity server package. It wires ASP.NET Identity, Entity Framework Core, and OpenIddict together, exposes opinionated Minimal APIs for registration/login/MFA, and supplies a fluent builder (`IdentityBaseBuilder`) so hosts can compose optional add-ons such as external providers or templated email senders. Every other package in the ecosystem layers on top of this foundation.

Key capabilities:
- Authorization-code PKCE, refresh tokens, and optional password grant support through OpenIddict.
- Registration, profile, password reset, email confirmation, logout, and MFA flows surfaced as Minimal APIs under `/auth` and `/users`.
- Configurable MFA challenge senders (email/SMS/authenticator apps) and templated email delivery.
- Health checks and diagnostics (`/healthz`) suitable for container readiness probes.
- Seed callbacks so hosts can run additional initialization logic after migrations.

## Installation & Wiring

Install from NuGet:

```bash
dotnet add package Identity.Base
```

Register the package in your identity host:

```csharp
var builder = WebApplication.CreateBuilder(args);

var identity = builder.Services.AddIdentityBase(builder.Configuration, builder.Environment);
identity
    .AddGoogleAuth()          // optional external providers
    .AddMicrosoftAuth()
    .AddAppleAuth();

var app = builder.Build();
app.UseApiPipeline(appBuilder => appBuilder.UseSerilogRequestLogging());
app.MapApiEndpoints();        // healthz, auth, users, email endpoints
await app.RunAsync();
```

`UseApiPipeline` adds HTTPS redirection, CORS, authentication/authorization middleware, and request correlation scopes. `MapApiEndpoints` registers the health check and Minimal API groups implemented by the package.

## Configuration

Options are bound automatically from `IConfiguration` (typically `appsettings.json`). Key option objects:

- `DatabaseOptions` – primary connection string and database provider selection.
- `RegistrationOptions` – profile fields, confirmation/password-reset URL templates.
- `MfaOptions` – enablement flags plus authenticator/email/SMS settings.
- `ExternalProviderOptions` – Google/Microsoft/Apple client ids, secrets, scopes.
- `OpenIddictOptions` – seeded clients/scopes, token lifetimes, signing keys.
- `OpenIddictServerKeyOptions` – key rotation policy.
- `CorsSettings` – allowed origins for SPA clients.
- `IdentitySeedOptions` – initial admin accounts and password policies.

Each option can be customised through `identity.Configure(options => ...)` or standard `IConfigureOptions<>` registrations.

## Public Surface

### Minimal API Endpoints

| Area | Routes | Notes |
| --- | --- | --- |
| Authentication | `/auth/register`, `/auth/login`, `/auth/logout`, `/auth/profile-schema` | Registration accepts extended profile metadata; login issues OpenIddict tokens; profile schema exposes configured fields. |
| MFA | `/auth/mfa/enroll`, `/auth/mfa/verify`, `/auth/mfa/challenge`, `/auth/mfa/disable`, `/auth/mfa/recovery-codes` | Supports authenticator app enrolment, on-demand email/SMS challenges (when enabled), and regeneration of recovery codes. |
| Email workflows | `/auth/confirm-email`, `/auth/resend-confirmation`, `/auth/forgot-password`, `/auth/reset-password` | Confirmation and password reset flows using templated email senders (`ITemplatedEmailSender`). |
| External providers | `/auth/external/{provider}/challenge`, `/auth/external/{provider}/callback`, `/auth/external/link` | OpenId Connect/OAuth provider integration with return URL validation. |
| User profile | `/users/me`, `/users/me/profile`, `/users/me/change-password` | Requires authentication via application cookie; updates profile metadata and password. |
| Health | `/healthz` | Aggregated health report suitable for Kubernetes/monitoring probes. |
| OpenIddict | `/connect/authorize`, `/connect/token`, `/connect/logout`, `/connect/userinfo` | Standard endpoints supplied by OpenIddict for OAuth/OIDC flows. |

### Public Types & Services
- `IdentityBaseBuilder` – fluent configuration surface with helpers such as `AddGoogleAuth`, `AfterIdentitySeed`, `ConfigureAppDbContextModel`.
- `IdentityBaseOptions` – capture additional extension configuration.
- EF Core models (`AppDbContext`, `ApplicationUser`) and option POCOs (RegistrationOptions, MfaOptions, etc.) for custom configuration binding.
- Extension methods: `services.AddIdentityBase`, `app.UseApiPipeline`, `endpoints.MapApiEndpoints`.

## Extension Points

Replace or extend behaviours via DI:
- Email delivery: implement `ITemplatedEmailSender` (or use `Identity.Base.Email.MailJet`).
- MFA channels: implement `IMfaChallengeSender` or register additional senders.
- Audit logging: implement `IAuditLogger` and/or `ILogSanitizer`.
- External login validation: override `IExternalReturnUrlValidator` or `IExternalCallbackUriFactory`.
- Seed callbacks: `identity.AfterIdentitySeed(...)` for post-migration tasks.
- Builders: call `identity.ConfigureAppDbContextModel(...)` to adjust EF Core models without forking the package.

## Dependencies & Compatibility
- Targets .NET 9 / ASP.NET Core 9 and EF Core 9.
- Provides the base required by `Identity.Base.Roles`, `Identity.Base.Organizations`, and `Identity.Base.Admin`.
- Works with `Identity.Base.Email.MailJet` for email, and `Identity.Base.AspNet` on consuming microservices.

## Examples & Guides
- [Getting Started](../../guides/getting-started.md)
- [Full-stack Integration Guide](../../guides/full-stack-integration-guide.md)
- [Identity Base Public API](../../reference/identity-base-public-api.md)

## Change Log
- See [CHANGELOG.md](../../CHANGELOG.md) (`Identity.Base` section)
