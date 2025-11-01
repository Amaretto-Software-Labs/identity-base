# Identity.Base.Email.MailJet

Optional Mailjet integration for Identity Base. This package provides:

- `MailJetEmailSender`, an `ITemplatedEmailSender` implementation that dispatches transactional emails via Mailjet.
- `MailJetOptions`, `MailJetTemplateOptions`, and `MailJetErrorReportingOptions` configuration types.
- Extension methods to register the sender for both raw `IServiceCollection` scenarios (`AddMailJetEmailSender`) and the `IdentityBaseBuilder` fluent API (`UseMailJetEmailSender`).

## Getting Started

1. Add the package to your host:
   ```bash
   dotnet add package Identity.Base.Email.MailJet
   ```
2. Register the sender when configuring services:
   ```csharp
   builder.Services.AddMailJetEmailSender(builder.Configuration);
   // or if you already have an IdentityBaseBuilder instance
   identityBuilder.UseMailJetEmailSender();
   ```
3. Configure the `MailJet` section in your appsettings (keys shown with example values):
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

If `MailJet.Enabled` is false the sender becomes a no-op, allowing you to disable outbound email in lower environments without changing code.

See `docs/guides/mailjet-email-sender.md` in the repository root for configuration guidance, template setup, and troubleshooting tips.
