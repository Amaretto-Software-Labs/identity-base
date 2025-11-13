# Identity Base – Lifecycle Hooks & Notification Augmentors Plan

## Summary
Identity Base currently has multiple narrow listener interfaces and fixed email payloads, making it difficult for hosts to react to lifecycle events (e.g., send a welcome email after confirmation) or customize notification templates. This plan introduces consolidated lifecycle listeners with before/after hooks plus a notification context augmentation pipeline so adopters can observe, veto, and enrich key flows without modifying core services or endpoints.

## Goals
- Provide first-class hooks for major user and organization transitions with the ability to stop the pipeline during “before” phases.
- Allow hosts to mutate notification payloads (templates, variables, channels) through an extensible pipeline before messages reach the transport.
- Keep Identity Base’s internal services unchanged while offering a clear, documented extension surface.
- Maintain backwards compatibility for existing listener implementations during migration.

## Scope
- New `IUserLifecycleListener` and `IOrganizationLifecycleListener` interfaces with default no-op methods for `Before*` and `After*` events (registration, confirmation, password reset, invitations, membership changes, deletion, restore, etc.).
- Context records capturing user/org data, tenant, locale, trigger metadata, correlation id, and flags describing notification state.
- Central lifecycle dispatcher that executes hooks, enforces ordering, and handles failure policy (bubble on “before” failures, configurable behavior for “after”).
- New notification context POCOs plus `INotificationContextAugmentor<TContext>` pipeline used by account email service, MFA sender, organization invitation sender, and future channels.
- Builder/DI extensions, options, migration shims, tests, and docs updates.

## Out of Scope
- Webhook delivery / outbox integration.
- New transport implementations (Mailjet continues as-is, just receives richer contexts).
- UI/UX changes to admin portals.
- Workflow/rules engines beyond the described hooks and augmentors.

## Design Overview

### Lifecycle Listener Overhaul
1. **Interfaces & Contexts**
   - `IUserLifecycleListener` / `IOrganizationLifecycleListener` expose paired `Before*Async` and `After*Async` methods.
   - Default interface implementations return `Task.CompletedTask` (requires C# 8+, satisfied by .NET 9 target) so adding future hooks is non-breaking.
   - Contexts (`UserLifecycleContext`, `OrganizationLifecycleContext`, `InvitationLifecycleContext`, etc.) are immutable snapshots describing the actor, target entity, tenant, correlation id, request metadata, and operation payload.

2. **Dispatcher**
   - `LifecycleDispatcher` (or two domain-specific dispatchers) resolves listeners via DI, enforces ordering, and handles exceptions.
   - `Before*` hooks execute sequentially; any thrown exception or explicit `LifecycleResult.Failure` stops the pipeline and surfaces a `LifecycleRejectedException`.
   - `After*` hooks run after the core operation completes. Failures default to “log and continue” but can be configured to bubble for critical operations via `LifecycleHookOptions`.

3. **Registration & Migration**
   - `IdentityBaseBuilder` gets `AddUserLifecycleListener<TListener>()` / `AddOrganizationLifecycleListener<TListener>()`.
   - Existing `IUserCreationListener`/`IUserUpdateListener`/etc. registrations are marked `[Obsolete]`. Temporary shim implementations forward events to the new interfaces so hosts can migrate gradually.
   - Core flows (registration endpoint, email confirmation, password reset, MFA enrollment, organization invitations/memberships, identity seeding) invoke the dispatcher instead of enumerating legacy listeners.

4. **Documentation & Testing**
   - New guide under `docs/guides` describing lifecycle hooks, veto patterns, and migration steps.
   - Unit tests covering: successful before/after execution, veto behavior, exception handling, options toggles, and diagnostics logging.

### Notification Context Augmentors
1. **Context Model**
   - Introduce mutable context types per notification (`EmailConfirmationContext`, `PasswordResetContext`, `OrganizationInvitationContext`, `EmailMfaChallengeContext`, etc.).
   - Properties include: template key, subject, branding, locale, base URLs, delivery channel (email/SMS), metadata flags, and a variables dictionary to populate template parameters.

2. **Augmentor Pipeline**
   - `INotificationContextAugmentor<TContext>` exposes `Task<NotificationAugmentResult> AugmentAsync(TContext ctx, CancellationToken token)` with a default success implementation.
   - Pipeline resolves augmentors in order (optionally via priority attribute) and stops if an augmentor returns `Failure` or throws. Failures bubble so hosting apps can decide whether to abort the surrounding operation or fallback.
   - Supports channel switching (e.g., mark context as SMS) and enrichment (locale-specific templates, additional variables, marketing copy).

3. **Integration**
   - Update `AccountEmailService`, `EmailMfaChallengeSender`, organization invitation sender, and any other templated email flows to:
     1. Build the appropriate context with defaults from configuration (template ids, URL formats, branding).
     2. Run the augmentor pipeline.
     3. Translate the final context into `TemplatedEmail` (or other transport payload) and send via `ITemplatedEmailSender`.
   - Provide builder extensions (`AddNotificationContextAugmentor<T>()`) and options for template id mappings (e.g., `MailJet:Templates:Welcome`, `MailJet:Templates:OrganizationInvite`).

4. **Documentation & Testing**
   - Docs covering augmentor registration, conflict resolution, and sample use cases (adding welcome copy, swapping templates per locale, forcing SMS).
   - Tests ensuring augmentors modify variables/template keys, can veto sending, and that fallback behavior is deterministic.

## Implementation Phases
1. **Foundations**
   - Create lifecycle interfaces, contexts, dispatcher, options, and DI extensions.
   - Add notification context abstractions and augmentor interface.

2. **Adoption**
   - Update registration/confirmation/password-reset/org flows to emit lifecycle hooks and run augmentors.
   - Implement shims for legacy listeners.

3. **Validation**
   - Add unit/integration tests.
   - Update docs (guides + API reference) and release notes.

4. **Cleanup**
   - Deprecate/remove legacy listener interfaces once all consumers migrate (timeline TBD).
   - Consider exposing sample implementations (welcome email listener, locale-based augmentor) in `docs/guides` or sample apps.

## Risks & Mitigations
- **Breaking changes** – mitigated via default interface implementations, shims, and clear migration docs.
- **Listener ordering conflicts** – provide deterministic ordering (registration order or explicit priority attribute).
- **Augmentor contention** – document conflict resolution rules; allow augmentors to mark context as handled to prevent overrides.
- **Performance/latency** – hooks and augmentors run in-process; emphasize best practices (fast, idempotent, respect cancellation).

## Success Criteria
- Hosts can send a welcome email after confirmation without modifying Identity Base endpoints.
- Hosts can add/override template variables or swap channels for invitation emails purely via augmentors.
- Existing listeners continue to work during migration with clear deprecation warnings.
- Tests and docs cover the new extension patterns.

