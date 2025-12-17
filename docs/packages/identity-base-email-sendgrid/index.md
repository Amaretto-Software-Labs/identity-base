# Identity.Base.Email.SendGrid

## Overview
`Identity.Base.Email.SendGrid` provides a production-ready `ITemplatedEmailSender` implementation backed by the SendGrid Mail Send API (dynamic templates). It enables Identity Base to send confirmation, password reset, and MFA challenge emails without custom plumbing.

## Installation & Wiring

```bash
dotnet add package Identity.Base.Email.SendGrid
```

Register the sender in your identity host:

```csharp
using Identity.Base.Email.SendGrid;

// When configuring services
builder.Services.AddSendGridEmailSender(builder.Configuration);
// or, if you already captured the IdentityBaseBuilder:
identityBuilder.UseSendGridEmailSender();
```

Once registered, Identity Base email flows (registration confirmation, password reset, MFA challenges) are routed through SendGrid. Organization invitation emails remain the host’s responsibility.

## Configuration

Add the `SendGrid` section to `appsettings.json` (values shown for illustration):

```json
{
  "SendGrid": {
    "Enabled": true,
    "ApiKey": "your-sendgrid-api-key",
    "FromEmail": "noreply@example.com",
    "FromName": "Identity Base",
    "Templates": {
      "Confirmation": "d-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
      "PasswordReset": "d-bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
      "MfaChallenge": "d-cccccccccccccccccccccccccccccccc"
    }
  }
}
```

Disable the sender in non-production environments by setting `SendGrid.Enabled` to `false` (the sender becomes a no-op and logs a warning when invoked).

## Template Variables

Identity Base supplies the following variables to templates:

- `email`
- `displayName`
- `confirmationUrl` (confirmation emails)
- `resetUrl` (password reset emails)
- `code` (email MFA challenge)

## Public Surface

- `AddSendGridEmailSender(this IServiceCollection, IConfiguration)` – registers options, validation, health checks, and replaces `ITemplatedEmailSender` with the SendGrid implementation.
- `UseSendGridEmailSender(this IdentityBaseBuilder)` – convenience extension for the fluent Identity Base builder.
- Options:
  - `SendGridOptions` (API key, sender identity, template IDs).
  - `SendGridTemplateOptions` (per-email template configuration).
- `SendGridOptionsValidator` – ensures configuration completeness when enabled.
- `SendGridOptionsHealthCheck` – health check that verifies configuration has been supplied (useful for readiness probes).

## Examples & Guides

- [SendGrid Email Sender Guide](../../guides/sendgrid-email-sender.md)
- [Getting Started](../../guides/getting-started.md#configure-email-delivery)

## Change Log

- See [CHANGELOG.md](../../CHANGELOG.md) (`Identity.Base.Email.SendGrid` entries)

