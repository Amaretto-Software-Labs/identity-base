# Passkeys

Identity Base supports passkeys for usernameless sign-in, existing-account enrollment, two signup policies, and passwordless-account recovery. The server uses ASP.NET Core Identity's .NET 10 passkey implementation; browser packages handle WebAuthn option conversion and credential serialization.

## Choose the signup policy

Hosts control which passkey signup choices a client may present:

| Mode | Account created with | Recovery |
| --- | --- | --- |
| `passkey-assisted` | Password and passkey | Existing password reset remains available |
| `passwordless` | Passkey only | Verified-email passkey replacement |

Set `Signup:EnabledModes` to either mode, both modes, or an empty array. The server enforces the selected mode; hiding an option in the UI is not a security boundary. The existing password-only `/auth/register` route is unchanged.

## Host configuration

```json
{
  "Passkeys": {
    "Enabled": true,
    "ServerDomain": "example.com",
    "AllowedOrigins": [
      "https://app.example.com",
      "https://identity.example.com"
    ],
    "AuthenticatorTimeoutSeconds": 180,
    "ChallengeSize": 32,
    "UserVerification": "required",
    "ResidentKey": "required",
    "Attestation": "none",
    "MaxPasskeysPerUser": 10,
    "NameMaxLength": 100,
    "Signup": {
      "EnabledModes": [
        "passkey-assisted",
        "passwordless"
      ],
      "ConfirmationUrlTemplate": "https://app.example.com/register/passkey?draftId={draftId}&token={token}",
      "DraftLifetimeMinutes": 30
    },
    "Recovery": {
      "ConfirmationUrlTemplate": "https://app.example.com/recover/passkey?draftId={draftId}&token={token}",
      "DraftLifetimeMinutes": 30
    }
  }
}
```

Every passkey origin must also be present in `Cors:AllowedOrigins`. Origins are exact—wildcards, paths, and query strings are rejected. Production requires HTTPS. `http://localhost[:port]` is accepted only in Development with `ServerDomain` set to `localhost`.

The RP domain must be the origin host or a parent suffix. Do not use a parent domain that also hosts untrusted tenant or user-controlled subdomains.
Use a strict Content Security Policy on every allowed login origin; any script executing there participates in the passkey security boundary.

## Database migration

Passkeys require .NET 10 Identity schema version 3. Generate and apply an `AppDbContext` migration from each consuming host before setting `Passkeys:Enabled` to `true`:

```bash
dotnet ef migrations add AddPasskeySupport \
  --context Identity.Base.Data.AppDbContext \
  --project Your.IdentityHost \
  --startup-project Your.IdentityHost

dotnet ef database update \
  --context Identity.Base.Data.AppDbContext \
  --project Your.IdentityHost \
  --startup-project Your.IdentityHost
```

The reference host includes checked PostgreSQL and SQL Server migrations. The migration adds user passkeys plus short-lived registration and recovery drafts; it does not rewrite existing login credentials.

To roll the feature back, first set `Passkeys:Enabled` to `false` and deploy. Keep the tables while any account may depend on passkeys. Only apply the migration `Down` after every passwordless account has another verified login/recovery path and retained credential data is no longer required.

## Client-core

`@identity-base/client-core` exposes browser-safe WebAuthn orchestration:

```ts
const configuration = await auth.getPasskeyConfiguration()

// Usernameless login
await auth.loginWithPasskey()

// Start either host-enabled signup mode
await auth.beginPasskeySignup({
  mode: 'passwordless', // or 'passkey-assisted'
  email: 'alice@example.com',
  metadata: { displayName: 'Alice' },
})

// On the email-link route:
await auth.confirmPasskeySignupEmail({ draftId, token })
await auth.completePasskeySignup({
  draftId,
  name: 'Alice laptop',
  // Required only for mode: 'passkey-assisted'
  password,
})
```

For passwordless recovery:

```ts
await auth.beginPasskeyRecovery({ email: 'alice@example.com' })
await auth.confirmPasskeyRecoveryEmail({ draftId, token })
await auth.completePasskeyRecovery({ draftId, name: 'Replacement passkey' })
```

Authenticated cookie sessions can call `listPasskeys`, `createPasskey`, `renamePasskey`, and `removePasskey`. Creation, rename, and removal deliberately require the application cookie; a bearer token alone cannot add or change a durable credential.

## React and Angular

React exports `usePasskeyLogin()` for login/signup/recovery and `usePasskeys()` for credential management:

```tsx
const { isSupported, login, beginSignup, completeSignup } = usePasskeyLogin()
const { passkeys, create, rename, remove } = usePasskeys()
```

Angular provides `IdentityPasskeyService` through `provideIdentityClient(...)`:

```ts
const passkeys = inject(IdentityPasskeyService)
await passkeys.login()
await passkeys.beginSignup({
  mode: 'passkey-assisted',
  email,
  metadata: { displayName },
})
```

All browser flows require a secure context and a WebAuthn-capable browser. Check `isPasskeySupported()` before rendering a passkey action.

The checked-in React sample runs on `http://localhost:5174`, which browsers
treat as a secure WebAuthn context. Its Vite development proxy forwards Identity
routes to the HTTPS reference host and makes the secure application cookie
same-origin for localhost testing. Deployments must serve the client over HTTPS
and must not remove the cookie's `Secure` attribute.

## Email and administration

Templated email senders receive these keys:

- `passkey.signup.confirmation`
- `passkey.recovery.confirmation`
- `passkey.recovery.completed`
- `passkey.reset`

MailJet and SendGrid may configure dedicated template IDs; when omitted, confirmation flows fall back to the normal confirmation template.

Administrators with `users.reset-passkeys` may call:

```http
POST /admin/users/{id}/passkeys/reset
Content-Type: application/json

{ "reason": "Lost device reported by user" }
```

The operation revokes all passkeys, rotates the security stamp, records the reason and revoked count in the audit log, and leaves password/external recovery methods unchanged.

## Security behavior

- Discoverable credentials and user verification are required.
- Email is verified before a signup or recovery ceremony can complete.
- Signup does not create a user until the email and passkey steps succeed.
- Passwordless recovery replaces old passkeys in one database transaction and rotates the security stamp.
- Email-only recovery lowers the account's effective phishing resistance to the security of that email account. Encourage passwordless users to register at least two passkeys; backup state is guidance, not proof of recoverability.
- Removing the last passkey is rejected when it would leave an account with no password or external login.
- Responses do not expose passkey public keys, attestation data, or signature counters.
- Begin/resend endpoints use enumeration-resistant `202 Accepted` responses and all ceremony endpoints are rate limited.

See the [passkey support specification](../plans/identity-base-passkey-support-spec.md) for endpoint contracts and threat-model detail.
