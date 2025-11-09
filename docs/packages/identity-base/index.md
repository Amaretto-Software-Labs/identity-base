# Identity.Base

## Overview
`Identity.Base` turns an ASP.NET Core 9 web application into a fully-fledged identity authority. It wires ASP.NET Identity, EF Core, and OpenIddict together, exposes Minimal APIs for account lifecycle (registration, login, logout, MFA, profile management), and offers a fluent builder (`IdentityBaseBuilder`) for composing optional integrations such as external providers or custom email senders. All other Identity Base packages rely on this foundation.

Key capabilities:
- **OpenIddict hosting** – authorization-code PKCE, refresh tokens, optional password grant, and configurable signing/encryption keys.
- **Identity workflows** – registration, login, logout, email confirmation, password reset, profile updates, MFA enrollment/challenge, recovery codes.
- **Operational readiness** – `/healthz` JSON health checks, opinionated middleware pipeline via `UseApiPipeline`, and seed callbacks for post-migration provisioning.

## Installation & Wiring

Install from NuGet:

```bash
dotnet add package Identity.Base
```

Register the package in your identity host:

```csharp
var builder = WebApplication.CreateBuilder(args);

var identity = builder.Services.AddIdentityBase(
    builder.Configuration,
    builder.Environment,
    configureDbContext: (sp, options) =>
    {
        var connectionString = sp.GetRequiredService<IConfiguration>().GetConnectionString("Primary")
            ?? throw new InvalidOperationException("ConnectionStrings:Primary must be set.");

        options.UseNpgsql(connectionString, sql => sql.EnableRetryOnFailure());
        // or options.UseSqlServer(connectionString);
    });

identity
    .UseTablePrefix("Contoso") // optional: set table prefix (defaults to Identity_)
    .AddConfiguredExternalProviders() // auto-configure providers enabled in config
    .UseTemplatedEmailSender<CustomEmailSender>(); // optional overrides

var app = builder.Build();
app.UseApiPipeline(appBuilder => appBuilder.UseSerilogRequestLogging());
app.MapApiEndpoints();
await app.RunAsync();
```

`UseApiPipeline` adds HTTPS redirection, the configured CORS policy, authentication/authorization, and a logging scope that tracks the correlation id and current user. `MapApiEndpoints` registers `/healthz` plus the Minimal APIs under `/auth`, `/users`, and `/email`.

### Example: register + login

```bash
# 1. Register (returns 202 Accepted after queuing a confirmation email)
curl -X POST https://localhost:5000/auth/register \
  -H "Content-Type: application/json" \
  -d '{
        "email": "alice@example.com",
        "password": "Passw0rd!",
        "metadata": { "displayName": "Alice" }
      }'

# 2. Attempt login (returns 200 when email is confirmed, 400 otherwise)
curl -X POST https://localhost:5000/auth/login \
  -H "Content-Type: application/json" \
  -d '{
        "email": "alice@example.com",
        "password": "Passw0rd!",
        "clientId": "spa-client"
      }'

# 3. Exchange the authorization code via OpenIddict once the SPA completes PKCE flow
curl -X POST https://localhost:5000/connect/token \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d 'grant_type=authorization_code&code=...&redirect_uri=https://app.example.com/auth/callback&client_id=spa-client&code_verifier=...'
```

## Configuration

Options are bound automatically from `IConfiguration`. The sections below are the most relevant:

| Section | Key fields | Default | Notes |
| --- | --- | --- | --- |
| `ConnectionStrings:Primary` | Database connection string | *(required)* | Used by `AppDbContext` and reused by add-ons if not overridden. Supports `InMemory:Name` for ephemeral scenarios. |
| `IdentitySeed` (`IdentitySeedOptions`) | `Enabled`, `Email`, `Password`, `Roles` | Disabled | When enabled, creates/updates a bootstrap admin user during startup and assigns the listed roles. |
| `Registration` (`RegistrationOptions`) | `ConfirmationUrlTemplate`, `PasswordResetUrlTemplate`, `ProfileFields` | Empty | Controls email templates and which metadata fields are collected and validated during registration. |
| `Mfa` (`MfaOptions`) | `Issuer`, `Email.Enabled`, `Sms.Enabled`, `Sms.AccountSid`, `Sms.AuthToken`, `Sms.FromPhoneNumber` | Email enabled, SMS disabled | SMS validation requires Twilio credentials. Tokens are issued with the configured `Issuer`. |
| `ExternalProviders` (`ExternalProviderOptions`) | Provider `Enabled` flag, `ClientId`, `ClientSecret`, `CallbackPath`, `Scopes` | Providers disabled | `identity.AddConfiguredExternalProviders()` registers any providers marked as enabled here (Google, Microsoft, Apple). |
| `OpenIddict` (`OpenIddictOptions`) | `Applications`, `Scopes`, token lifetimes | SPA (`spa-client`) and confidential (`test-client`) seeded | Configure additional client ids/redirect URIs or adjust lifetimes. |
| `OpenIddict:Server:Keys` (`OpenIddictServerKeyOptions`) | Signing/encryption key descriptors | Runtime-generated | Override to use persisted keys or external key stores. |
| `Cors` (`CorsSettings`) | `AllowedOrigins`, `AllowCredentials` | Empty | `UseApiPipeline` consumes this to create the default CORS policy for Minimal APIs. |

Use `identity.UseTablePrefix("Contoso")` if you want every EF Core table created by Identity Base (and add-on packages) to use a different prefix than the default `Identity_`.
Customisation examples:

```csharp
identity.ConfigureAppDbContextModel(modelBuilder =>
{
    modelBuilder.Entity<ApplicationUser>()
        .HasIndex(u => u.Email)
        .HasDatabaseName("IX_AppUser_Email");
});

identity.AfterIdentitySeed(async (sp, ct) =>
{
    var logger = sp.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("Identity seed completed");
});
```

## Public Surface

### Minimal API Endpoints

| Area | Routes | Behaviour |
| --- | --- | --- |
| Authentication | `/auth/register`, `/auth/login`, `/auth/logout`, `/auth/profile-schema` | `/auth/register` → `202 Accepted` with `{ correlationId }` once the confirmation email is queued. `/auth/login` → `200 OK` with `{ message, clientId }`, or `{ requiresTwoFactor: true, methods: [...] }` when MFA is required. `/auth/logout` clears the Identity cookie. `/auth/profile-schema` returns the configured registration fields. |
| MFA | `/auth/mfa/enroll`, `/auth/mfa/verify`, `/auth/mfa/challenge`, `/auth/mfa/disable`, `/auth/mfa/recovery-codes` | Enrollment returns the shared key + otpauth URI. Verify accepts authenticator/email/SMS/recovery codes, subject to `MfaOptions`. Challenge dispatches an email/SMS challenge when that channel is enabled. Recovery codes returns a fresh array of codes. |
| Email workflows | `/auth/confirm-email`, `/auth/resend-confirmation`, `/auth/forgot-password`, `/auth/reset-password` | Tokens (`token`, `userId`) are Base64URL encoded. Resend confirmation is idempotent and returns `202 Accepted` even for unknown email addresses. Forgot password returns `202 Accepted`; reset returns `200 OK` on success. |
| External providers | `/auth/external/{provider}/challenge`, `/auth/external/{provider}/callback`, `/auth/external/link` | Supported providers: Google, Microsoft, Apple. `IExternalReturnUrlValidator` guards against open redirects; override for custom whitelists. |
| User profile | `/users/me`, `/users/me/profile`, `/users/me/change-password` | Requires the application cookie (issued automatically after the initial SPA login). Profile updates validate the `concurrencyStamp` and use `IAuditLogger` to record changes. |
| Health | `/healthz` | Returns `{ "status": "Healthy", "checks": [{ "name": "database", "status": "Healthy", ... }] }`. Additional checks from add-ons appear in the `checks` array. |
| OpenIddict | `/connect/authorize`, `/connect/token`, `/connect/logout`, `/connect/userinfo` | Standard OpenIddict endpoints used by the SPA PKCE flow and the optional password grant. |

### Builders, Services, and Types
- `IdentityBaseBuilder` fluent helpers: `ConfigureAppDbContextModel`, `ConfigureIdentityRolesModel`, `UseTemplatedEmailSender`, `AfterRoleSeeding`, `AfterIdentitySeed`, `AddConfiguredExternalProviders`, individual provider helpers (`AddGoogleAuth`, `AddMicrosoftAuth`, `AddAppleAuth`).
- EF Core artefacts: `AppDbContext`, `ApplicationUser`, and fluent configuration hooks to extend the model.
- Core services: `IAccountEmailService` (confirmation/reset emails), `IMfaChallengeSender`, `IAuditLogger`, `ILogSanitizer`, `IExternalReturnUrlValidator`, `IExternalCallbackUriFactory`.
- Extension methods: `services.AddIdentityBase`, `app.UseApiPipeline`, `endpoints.MapApiEndpoints`.

## Extension Points

- **Email delivery** – swap `ITemplatedEmailSender` (e.g., install `Identity.Base.Email.MailJet` or implement your own).
- **MFA channels** – register custom `IMfaChallengeSender` implementations; the default supports authenticator apps plus email/SMS (when enabled in configuration).
- **Audit logging & sanitisation** – override `IAuditLogger` and `ILogSanitizer` to integrate with your logging stack or redact additional fields.
- **External provider plumbing** – enable providers via configuration and call `identity.AddConfiguredExternalProviders()` or customise each via `AddGoogleAuth`/`AddMicrosoftAuth`/`AddAppleAuth`.
- **Seeding callbacks** – use `AfterRoleSeeding` and `AfterIdentitySeed` to run arbitrary provisioning steps once the core seeds finish.
- **EF Core model customisation** – `ConfigureAppDbContextModel` and `ConfigureIdentityRolesModel` let you add indexes/shadow properties without forking the package.

## Dependencies & Compatibility
- Requires .NET 9 / ASP.NET Core 9 and EF Core 9.
- Forms the basis for all other packages: RBAC (`Identity.Base.Roles`), organizations, admin, MailJet, ASP.NET helpers, React clients.
- Compatible with PostgreSQL, SQL Server, and in-memory providers via standard EF Core configuration.

## Troubleshooting & Tips
- **Email not confirmed** – `/auth/login` returns `400` with `Email must be confirmed...` until `/auth/confirm-email` succeeds. Use `/auth/resend-confirmation` to resend the token.
- **MFA challenge blocked** – `/auth/mfa/challenge` returns `400` with `SMS MFA challenge is disabled.` if `Mfa:Sms:Enabled` is false or credentials are missing.
- **Unknown client id** – `/auth/login` fails with `Unknown client_id.` unless the client exists under `OpenIddict:Applications` (or is the seeded SPA/confidential client).
- **CORS issues** – ensure every SPA origin is listed under `Cors:AllowedOrigins`. The pipeline uses the default policy for all Minimal APIs.
- **Applying migrations** – generate and apply migrations from your host project (`dotnet ef migrations add InitialIdentityBase --context AppDbContext`) before running the app; Identity Base no longer applies migrations automatically.

## Examples & Guides
- [Getting Started](../../guides/getting-started.md)
- [Full-stack Integration Guide](../../guides/full-stack-integration-guide.md)
- [Identity Base Public API Reference](../../reference/identity-base-public-api.md)
- Sample host implementation: `Identity.Base.Host/Program.cs`

## Change Log
- See [CHANGELOG.md](../../CHANGELOG.md) (`Identity.Base` section)
