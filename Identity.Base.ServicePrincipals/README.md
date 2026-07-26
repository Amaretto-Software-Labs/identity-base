# Identity.Base.ServicePrincipals

Opt-in managed machine identities for Identity Base. Register with
`AddIdentityBaseServicePrincipals(...)` after Identity Base/Admin and map
`MapIdentityBaseServicePrincipalEndpoints()`. The host owns migrations for
`ServicePrincipalDbContext` and the extended `IdentityRolesDbContext`.

Managed clients use multiple independently revocable hashed credentials while
legacy configuration-seeded `client_credentials` clients continue to use the
standard OpenIddict application secret.

Admin creation accepts a display name only. Identity Base generates an immutable,
unique `client_id` from a lowercase kebab-case display-name prefix plus a
cryptographically random suffix.

Managed access tokens use the configured `Identity:ServicePrincipals:AccessTokenLifetime`
(15 minutes by default) and do not receive refresh tokens. Hosts can register
`IServicePrincipalLifecycleListener` implementations to enforce product-specific
cleanup or governance before a principal is disabled. A listener can reject the
operation with a conflict response by throwing `InvalidOperationException`; other
exception types are treated as unexpected failures.
