# SendGrid Email Sender Guide

This repository ships an optional SendGrid email sender package (`Identity.Base.Email.SendGrid`) that implements `ITemplatedEmailSender` using SendGrid's Mail Send API with dynamic templates.

> Quick start: `dotnet add package Identity.Base.Email.SendGrid` and call `identity.UseSendGridEmailSender();` when configuring services.

## What it does

- Sends Identity Base transactional emails via SendGrid:
  - email confirmation
  - password reset
  - email MFA challenges
- Maps `TemplatedEmail.TemplateKey` values (`identity.account.confirmation`, `identity.password.reset`, `identity.mfa.email.challenge`) to the configured SendGrid template IDs.
- Passes the notification variables through as `dynamic_template_data`.

## Install

```bash
dotnet add package Identity.Base.Email.SendGrid
```

## Wire it into your host

```csharp
using Identity.Base.Email.SendGrid;

// When configuring services
builder.Services.AddSendGridEmailSender(builder.Configuration);
// or, if you already captured the IdentityBaseBuilder:
identityBuilder.UseSendGridEmailSender();
```

## Configuration

Add the `SendGrid` section to your configuration:

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

If `SendGrid.Enabled` is `false`, the sender will skip outbound email and log a warning so you can keep flows enabled locally without calling the SendGrid API.

## Template variables (dynamic_template_data)

Identity Base provides these keys:

- Confirmation email: `email`, `displayName`, `confirmationUrl`
- Password reset email: `email`, `displayName`, `resetUrl`
- Email MFA challenge: `email`, `displayName`, `code`

## Health checks

When registered, the package adds a `sendgrid` health check. It reports:
- `Healthy` when disabled
- `Degraded` when enabled but missing required values

