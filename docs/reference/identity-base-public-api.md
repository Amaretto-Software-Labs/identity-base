# Identity.Base Public Surface

> For the full package guide (installation, configuration, endpoint overview) see [docs/packages/identity-base/index.md](../packages/identity-base/index.md). This reference focuses on the raw public types that remain available for host applications.

The NuGet package exposes a small set of entry points intended for host composition and extension.

## Composition
- `IdentityBaseBuilder` – returned from `services.AddIdentityBase(...)` for fluent configuration.
- `IdentityBaseOptions` – allows hosts to override option binding before the builder wires dependencies.
- `UseTablePrefix(string tablePrefix)` – configures the table prefix used by all Identity Base EF Core contexts (defaults to `Identity_`).

## Options & Configuration Models
These remain `public` so consumers can author custom `IConfigureOptions<>` implementations or validate configuration:
- `RegistrationOptions` / `RegistrationProfileFieldOptions`
- `MfaOptions` / `EmailChallengeOptions` / `SmsChallengeOptions`
- `OpenIddictOptions`, `OpenIddictApplicationOptions`, `OpenIddictScopeOptions`
- `OpenIddictServerKeyOptions` plus nested descriptors
- `CorsSettings`
- `IdentitySeedOptions`

## Identity & Data Models
- `ApplicationUser`, `ApplicationRole`
- `AppDbContext`
- `UserProfileMetadata`
- Types supplied by `Identity.Base.Roles`:
  - `Role`, `Permission`, `RolePermission`, `UserRole`, `ServicePrincipalRole`
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
- `IUserLifecycleListener`
- `IOrganizationLifecycleListener` (via `IdentityBaseOrganizationsBuilder`)
- `IServicePrincipalLifecycleListener` (via `Identity.Base.ServicePrincipals`)
- `INotificationContextAugmentor<TContext>`

The library keeps concrete implementations (`TwilioMfaChallengeSender`, `AuditLogger`, etc.) internal while still registering them by default through the builder. Email providers ship as optional packages (for example, `Identity.Base.Email.MailJet`). This limits the public API to dependency boundaries that hosts are expected to customise.
- `IdentityBaseBuilder.Services` and `.Configuration` (introduced to support optional add-ons such as Mailjet).

### Builder helpers
- `IdentityBaseBuilder.AddExternalAuthProvider(...)`
- `IdentityBaseBuilder.AddUserLifecycleListener<TListener>()`
- `IdentityBaseBuilder.AddNotificationContextAugmentor<TContext, TAugmentor>()`
- `IdentityBaseOrganizationsBuilder.AddOrganizationLifecycleListener<TListener>()`
- `AddIdentityBaseServicePrincipals(...)` / `MapIdentityBaseServicePrincipalEndpoints()`
- `IdentityBaseServicePrincipalsBuilder.ConfigureModel(...)` / `UseDbContext<TContext>()`

## Service Principal Package Surface

`Identity.Base.ServicePrincipals` intentionally exposes the types hosts need for composition and custom workflows:

- `ServicePrincipalDbContext`, `ServicePrincipal`, and `ServicePrincipalCredential`
- `ServicePrincipalService`
- admin request/response records in `Identity.Base.ServicePrincipals.Api`
- `ServicePrincipalOptions` and `ServicePrincipalPermissions`
- `IServicePrincipalLifecycleListener`

The core `Identity.Base` package also exposes `IClientCredentialsPrincipalProvider` and `IManagedClientCredentialsClientResolver` so other packages can provide managed client-credentials identities without replacing the grant handler.
* [Lifecycle Hooks & Notification Augmentors](../plans/identity-base-lifecycle-hooks-and-notification-augmentors.md)
