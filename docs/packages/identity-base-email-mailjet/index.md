# Identity.Base.Email.MailJet

## Overview
`Identity.Base.Email.MailJet` provides a production-ready `ITemplatedEmailSender` implementation backed by the Mailjet transactional email API. It enables Identity Base to send confirmation, password reset, and MFA challenge emails without custom plumbing. The package also surfaces options for template IDs, from-address configuration, and optional error-reporting hooks.

## Installation & Wiring

```bash
dotnet add package Identity.Base.Email.MailJet
```

Register the sender in your identity host:

```csharp
using Identity.Base.Email.MailJet;

// When configuring services
builder.Services.AddMailJetEmailSender(builder.Configuration);
// or, if you already captured the IdentityBaseBuilder:
identityBuilder.UseMailJetEmailSender();
```

Once registered, Identity Base email flows (registration confirmation, password reset, MFA challenges) are routed through Mailjet. Organization invitation emails remain the host’s responsibility.

## Configuration

Add the `MailJet` section to `appsettings.json` (values shown for illustration):

```json
{
  "MailJet": {
    "Enabled": true,
    "ApiKey": "your-mailjet-api-key",
    "ApiSecret": "your-mailjet-api-secret",
    "FromEmail": "noreply@example.com",
    "FromName": "Identity Base",
    "Templates": {
      "Confirmation": 1000000,
      "PasswordReset": 1000001,
      "MfaChallenge": 1000002
    },
    "ErrorReporting": {
      "Enabled": true,
      "Email": "identity-alerts@example.com"
    }
  }
}
```

Disable the sender in non-production environments by setting `MailJet.Enabled` to `false` (the package falls back to a no-op implementation). Organization invitation mails are still orchestrated by the host application—use the templated sender directly if you want to deliver those messages from Mailjet as well.

## Public Surface

- `AddMailJetEmailSender(this IServiceCollection, IConfiguration)` – registers options, validation, health checks, and replaces `ITemplatedEmailSender` with the Mailjet implementation.
- `UseMailJetEmailSender(this IdentityBaseBuilder)` – convenience extension for the fluent Identity Base builder.
- Options:
  - `MailJetOptions` (API keys, sender identity, template IDs, error reporting configuration).
  - `MailJetTemplateOptions` (per-email template configuration).
  - `MailJetErrorReportingOptions` (optional failure notifications).
- `MailJetOptionsValidator` – ensures configuration completeness at startup.
- `MailJetOptionsHealthCheck` – health check that verifies configuration has been supplied (useful for readiness probes).

## Extension Points

- Replace `MailJetEmailSender` with a custom implementation by registering your own `ITemplatedEmailSender`.
- Use configuration providers (secret stores, environment variables) to inject API credentials securely.

## Dependencies & Compatibility

- Depends on `Identity.Base`.
- Coexists with other email senders—only one `ITemplatedEmailSender` should be registered at a time.
- Template variables align with Identity Base token payloads (`{token}`, `{userId}`, etc.).

## Troubleshooting & Tips
- **Health check failing** – ensure `MailJet.Enabled` is `true` and all required fields (`ApiKey`, `ApiSecret`, `FromEmail`, template ids) are populated. The `mailjet` health check fails fast when any required value is missing.
- **Template errors** – enable `ErrorReporting` to receive Mailjet’s template error notifications. The sender logs failures through `ILogger<MailJetEmailSender>`; enable debug logging for more details during development.
- **Disable in development** – keep `MailJet.Enabled = false` for local environments to avoid hitting the Mailjet API while still exercising Identity Base flows (a no-op sender writes to the console).

## Examples & Guides

- [Mailjet Email Sender Guide](../../guides/mailjet-email-sender.md)
- [Getting Started](../../guides/getting-started.md#configure-email-delivery)
- Playbook: ../../playbooks/enable-mailjet-email-sender.md

## Change Log

- See [CHANGELOG.md](../../CHANGELOG.md) (`Identity.Base.Email.MailJet` entries)
