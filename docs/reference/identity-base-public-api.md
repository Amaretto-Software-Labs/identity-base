# Identity.Base Public Surface

> For the full package guide (installation, configuration, endpoint overview) see [docs/packages/identity-base/index.md](../packages/identity-base/index.md). This reference focuses on the raw public types that remain available for host applications.

The NuGet package exposes a small set of entry points intended for host composition and extension.

## Composition
- `IdentityBaseBuilder` – returned from `services.AddIdentityBase(...)` for fluent configuration.
- `IdentityBaseOptions` – allows hosts to override option binding before the builder wires dependencies.

## Options & Configuration Models
These remain `public` so consumers can author custom `IConfigureOptions<>` implementations or validate configuration:
- `DatabaseOptions`
- `RegistrationOptions` / `RegistrationProfileFieldOptions`
- `MfaOptions` / `EmailChallengeOptions` / `SmsChallengeOptions`
- `ExternalProviderOptions` / provider-specific option records
- `OpenIddictOptions`, `OpenIddictApplicationOptions`, `OpenIddictScopeOptions`
- `OpenIddictServerKeyOptions` plus nested descriptors
- `CorsSettings`
- `IdentitySeedOptions`

## Identity & Data Models
- `ApplicationUser`, `ApplicationRole`
- `AppDbContext` and design-time `AppDbContextFactory`
- `UserProfileMetadata`
- Planned additions via `Identity.Base.Roles`:
  - `Role`, `Permission`, `RolePermission`, `UserRole`
  - `AuditEntry` (configurable)
  - See `rbac-design.md` for schema details

## Extension Interfaces
Consumers can replace default services by implementing the following interfaces:
- `ITemplatedEmailSender`
- `IMfaChallengeSender`
- `IAuditLogger`
- `ILogSanitizer`
- `IExternalReturnUrlValidator`
- `IExternalCallbackUriFactory`

The library keeps concrete implementations (`TwilioMfaChallengeSender`, `AuditLogger`, etc.) internal while still registering them by default through the builder. Email providers ship as optional packages (for example, `Identity.Base.Email.MailJet`). This limits the public API to dependency boundaries that hosts are expected to customise.
- `IdentityBaseBuilder.Services` and `.Configuration` (introduced to support optional add-ons such as Mailjet).
