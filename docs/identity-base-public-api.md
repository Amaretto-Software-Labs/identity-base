# Identity.Base Public Surface

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
- `MailJetOptions`
- `OpenIddictOptions`, `OpenIddictApplicationOptions`, `OpenIddictScopeOptions`
- `OpenIddictServerKeyOptions` plus nested descriptors
- `CorsSettings`
- `IdentitySeedOptions`

## Identity & Data Models
- `ApplicationUser`, `ApplicationRole`
- `AppDbContext` and design-time `AppDbContextFactory`
- `UserProfileMetadata`

## Extension Interfaces
Consumers can replace default services by implementing the following interfaces:
- `ITemplatedEmailSender`
- `IMfaChallengeSender`
- `IAuditLogger`
- `ILogSanitizer`
- `IExternalReturnUrlValidator`
- `IExternalCallbackUriFactory`

The library now keeps concrete implementations (`MailJetEmailSender`, `TwilioMfaChallengeSender`, `AuditLogger`, etc.) internal while still registering them by default through the builder. This limits the public API to dependency boundaries that hosts are expected to customise.
