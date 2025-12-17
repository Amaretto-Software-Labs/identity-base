# Identity.Base.Email.SendGrid

> Comprehensive documentation is available at [docs/packages/identity-base-email-sendgrid/index.md](../docs/packages/identity-base-email-sendgrid/index.md). The README below is a condensed quick start.

Optional SendGrid integration for Identity Base. This package provides:

- `SendGridEmailSender`, an `ITemplatedEmailSender` implementation that dispatches transactional emails via SendGrid.
- `SendGridOptions` and `SendGridTemplateOptions` configuration types.
- Extension methods to register the sender for both raw `IServiceCollection` scenarios (`AddSendGridEmailSender`) and the `IdentityBaseBuilder` fluent API (`UseSendGridEmailSender`).

## Getting Started

1. Add the package to your host:
   ```bash
   dotnet add package Identity.Base.Email.SendGrid
   ```
2. Register the sender when configuring services:
   ```csharp
   builder.Services.AddSendGridEmailSender(builder.Configuration);
   // or if you already have an IdentityBaseBuilder instance
   identityBuilder.UseSendGridEmailSender();
   ```
3. Configure the `SendGrid` section in your appsettings (keys shown with example values):
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

If `SendGrid.Enabled` is false the sender becomes a no-op, allowing you to disable outbound email in lower environments without changing code.

See `docs/guides/sendgrid-email-sender.md` in the repository root for configuration guidance, template setup, and troubleshooting tips.

