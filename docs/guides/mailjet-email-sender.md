# Mailjet Email Sender Guide

This repository ships an optional Mailjet email sender package (`Identity.Base.Email.MailJet`) that implements `ITemplatedEmailSender` using Mailjet's transactional send API.

> Quick start: `dotnet add package Identity.Base.Email.MailJet` and call `identity.UseMailJetEmailSender();` when configuring services.

## What it does

- Sends Identity Base transactional emails via Mailjet:
  - email confirmation
  - password reset
  - email MFA challenges
- Maps `TemplatedEmail.TemplateKey` values (`identity.account.confirmation`, `identity.password.reset`, `identity.mfa.email.challenge`) to the configured Mailjet template IDs.
- Passes the notification variables through as Mailjet `Variables`.

## Install

```bash
dotnet add package Identity.Base.Email.MailJet
```

## Wire it into your host

```csharp
using Identity.Base.Email.MailJet;

// When configuring services
builder.Services.AddMailJetEmailSender(builder.Configuration);
// or, if you already captured the IdentityBaseBuilder:
identityBuilder.UseMailJetEmailSender();
```

## Configuration

Add the `MailJet` section to your configuration:

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

If `MailJet.Enabled` is `false`, the sender will skip outbound email and log a warning so you can keep flows enabled locally without calling the Mailjet API.

## Template variables

Identity Base provides these keys:

- Confirmation email: `email`, `displayName`, `confirmationUrl`
- Password reset email: `email`, `displayName`, `resetUrl`
- Email MFA challenge: `email`, `displayName`, `code`

## Health checks

When registered, the package adds a `mailjet` health check. It reports:
- `Healthy` when disabled
- `Degraded` when enabled but missing required values

