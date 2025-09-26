# MailJet Email Sender Implementation Guide

This document explains how to implement a MailJet-backed email sender in an ASP.NET Core application. It includes enough detail that a junior engineer can recreate the integration, configure templated emails, and troubleshoot common issues. The guidance is intentionally project-agnostic—adapt the file paths and namespaces to match your solution structure.

## Dependencies
- `Mailjet.Api` NuGet package version `3.0.0` (brings in `Mailjet.Client` and transactional email builders).
- `Microsoft.AspNetCore.Identity.UI.Services` (or another interface that exposes `SendEmailAsync`).
- Standard ASP.NET Core logging and options packages (`Microsoft.Extensions.Logging`, `Microsoft.Extensions.Options`).

Add the MailJet SDK to the project that hosts the sender:

```bash
dotnet add package Mailjet.Api --version 3.0.0
```

## Key Components
- `Services/MailJetEmailSender.cs`: Contains the `MailJetEmailSender` class together with helper types such as `MailJetOptions`, `MailJetTemplates`, and the `EmailTemplate` enum.
- Configuration binding so the sender picks up API credentials, from address, and template IDs.
- Registration in dependency injection so the implementation is available via `IEmailSender`/`ITemplatedEmailSender`.

## Dependency Injection Registration
Register the sender and bind configuration in your `Program.cs` (or, preferably, the dedicated dependency-injection extension/module that the app exposes for service wiring):

```csharp
builder.Services.Configure<MailJetOptions>(builder.Configuration.GetSection("MailJet"));
builder.Services.AddTransient<IEmailSender, MailJetEmailSender>();
builder.Services.AddTransient<ITemplatedEmailSender, MailJetEmailSender>();
```

Any component that resolves `IEmailSender` will automatically use this MailJet implementation. For scenarios that require template support, inject the richer `ITemplatedEmailSender` interface defined in `MailJetEmailSender.cs`.

### Variables available to templates

- `confirmLink` / `verificationLink` — absolute URL the user follows to confirm their email address.
- `displayName` — friendly name for greetings.
- `communityName` / `communitySlug` — provided during president self-registration for tailored messaging.
- `documentId` / `document.Title` / `publishedAt` — included with the document publication notification template.

## Configuration Structure
Provide MailJet credentials, sender identity, and template IDs through configuration (e.g., `appsettings.json`, environment variables, or a secret store). A typical configuration section looks like:

```json
{
  "MailJet": {
    "ApiKey": "YOUR_MAILJET_API_KEY",
    "ApiSecret": "YOUR_MAILJET_API_SECRET",
    "FromEmail": "no-reply@your-domain.com",
    "FromName": "Your Product",
    "Templates": {
      "EmailConfirmation": 1000001,
      "PasswordReset": 1000002,
      "EmailChangeNotification": 1000003,
      "EmailChangeConfirmation": 1000004,
      "AccountDeletionNotification": 1000005,
      "TwoFactorEnabled": 1000006,
      "TwoFactorDisabled": 1000007,
      "FormClaimVerification": 1000008,
      "FormClaimSuccess": 1000009
    }
  }
}
```

The numeric template IDs must match the templates created inside your MailJet account. Any entry left at `0` (the default in `MailJetTemplates`) is treated as "not configured".

## Template-First Workflow (Preferred)
The sender is designed around reusable MailJet templates. Always aim to deliver emails through the templated workflow so that:
- Content authors can edit copy and layout directly within MailJet.
- Translations and branding changes do not require code deployments.
- Rendering logic is centralised and easier to QA.

Ad-hoc HTML bodies are no longer allowed—if a template is missing, the sender throws so the configuration can be fixed.

## Supported Templates
`MailJetEmailSender.cs` defines the `EmailTemplate` enum and the matching `MailJetTemplates` option bag:

```csharp
public enum EmailTemplate
{
    EmailConfirmation,
    PasswordReset,
    EmailChangeNotification,
    EmailChangeConfirmation,
    AccountDeletionNotification,
    TwoFactorEnabled,
    TwoFactorDisabled,
    FormClaimVerification,
    FormClaimSuccess
}
```

Each enum value maps to a property on `MailJetTemplates`. When you add a new template type, extend both the enum and the options class, then supply the MailJet template ID through configuration.

## Constructor and Dependencies
`MailJetEmailSender` is lightweight: options and logging are injected through the constructor, and a `MailjetClient` is created on demand for each send operation.

```csharp
public MailJetEmailSender(IOptions<MailJetOptions> options, ILogger<MailJetEmailSender> logger)
{
    _options = options.Value;
    _logger = logger;
}
```

## Reference Implementation
The following excerpt shows a complete sender that enforces the template-only policy. Drop this into your project (adjust namespaces and the error-reporting address) as a starting point.

```csharp
using Mailjet.Client;
using Mailjet.Client.TransactionalEmails;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

public enum EmailTemplate
{
    EmailConfirmation,
    PasswordReset,
    EmailChangeNotification,
    EmailChangeConfirmation,
    AccountDeletionNotification,
    TwoFactorEnabled,
    TwoFactorDisabled,
    FormClaimVerification,
    FormClaimSuccess
}

public interface ITemplatedEmailSender : IEmailSender
{
    Task SendTemplatedEmailAsync(string email, EmailTemplate template, object? variables = null);
}

public sealed class MailJetOptions
{
    public required string ApiKey { get; set; }
    public required string ApiSecret { get; set; }
    public required string FromEmail { get; set; }
    public required string FromName { get; set; }
    public MailJetTemplates Templates { get; set; } = new();
}

public sealed class MailJetTemplates
{
    public int EmailConfirmation { get; set; }
    public int PasswordReset { get; set; }
    public int EmailChangeNotification { get; set; }
    public int EmailChangeConfirmation { get; set; }
    public int AccountDeletionNotification { get; set; }
    public int TwoFactorEnabled { get; set; }
    public int TwoFactorDisabled { get; set; }
    public int FormClaimVerification { get; set; }
    public int FormClaimSuccess { get; set; }
}

public sealed class MailJetEmailSender : ITemplatedEmailSender
{
    private readonly MailJetOptions _options;
    private readonly ILogger<MailJetEmailSender> _logger;

    public MailJetEmailSender(IOptions<MailJetOptions> options, ILogger<MailJetEmailSender> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public Task SendEmailAsync(string email, string subject, string htmlMessage)
    {
        _logger.LogError("Attempted to send non-templated email to {Email}. Configure a template instead.", email);
        throw new NotSupportedException("Plain HTML emails are not supported. Configure a MailJet template and use SendTemplatedEmailAsync.");
    }

    public async Task SendTemplatedEmailAsync(string email, EmailTemplate template, object? variables = null)
    {
        var templateId = GetTemplateId(template);

        if (templateId == 0)
        {
            _logger.LogError("Template {Template} is not configured (ID: 0) for {Email}", template, email);
            throw new InvalidOperationException($"Template {template} is not configured. Configure the template ID before sending.");
        }

        var client = new MailjetClient(_options.ApiKey, _options.ApiSecret);

        var messageBuilder = new TransactionalEmailBuilder()
            .WithFrom(new SendContact(_options.FromEmail, _options.FromName))
            .WithTo(new SendContact(email))
            .WithTemplateLanguage(true)
            .WithTemplateId(templateId)
            .WithTemplateErrorReporting(new SendContact("ops@example.com")); // Replace with your own monitoring inbox.

        if (variables != null)
        {
            var variableDictionary = ConvertVariablesToDictionary(variables);
            _logger.LogInformation("Template variables for {Template} to {Email}: {@Variables}", template, email, variableDictionary);
            messageBuilder.WithVariables(variableDictionary);
        }

        var response = await client.SendTransactionalEmailAsync(messageBuilder.Build());

        if (response.Messages == null || response.Messages.Length == 0)
        {
            _logger.LogWarning("Templated email to {Email} using template {Template} (ID: {TemplateId}) may not have been sent - no response messages", email, template, templateId);
            return;
        }

        _logger.LogInformation("Templated email sent to {Email} using template {Template} (ID: {TemplateId}): {Status}",
            email, template, templateId, response.Messages[0].Status);
    }

    private int GetTemplateId(EmailTemplate template) => template switch
    {
        EmailTemplate.EmailConfirmation => _options.Templates.EmailConfirmation,
        EmailTemplate.PasswordReset => _options.Templates.PasswordReset,
        EmailTemplate.EmailChangeNotification => _options.Templates.EmailChangeNotification,
        EmailTemplate.EmailChangeConfirmation => _options.Templates.EmailChangeConfirmation,
        EmailTemplate.AccountDeletionNotification => _options.Templates.AccountDeletionNotification,
        EmailTemplate.TwoFactorEnabled => _options.Templates.TwoFactorEnabled,
        EmailTemplate.TwoFactorDisabled => _options.Templates.TwoFactorDisabled,
        EmailTemplate.FormClaimVerification => _options.Templates.FormClaimVerification,
        EmailTemplate.FormClaimSuccess => _options.Templates.FormClaimSuccess,
        _ => 0
    };

    private static Dictionary<string, object> ConvertVariablesToDictionary(object variables)
    {
        var dictionary = new Dictionary<string, object>();

        if (variables == null)
        {
            return dictionary;
        }

        foreach (var property in variables.GetType().GetProperties())
        {
            var value = property.GetValue(variables);
            if (value != null)
            {
                dictionary[property.Name] = value;
            }
        }

        dictionary.Add("submittedAt", DateTime.UtcNow.ToLongDateString());
        return dictionary;
    }
}
```

## Non-Templated Emails Are Disabled
`SendEmailAsync` still exists to satisfy the `IEmailSender` interface, but it now throws a `NotSupportedException`. This guards against accidentally authoring HTML bodies in code. All callers must provision and use a MailJet template instead.

```csharp
public Task SendEmailAsync(string email, string subject, string htmlMessage)
{
    _logger.LogError("Attempted to send non-templated email to {Email}", email);
    throw new NotSupportedException("Plain HTML emails are not supported. Configure a MailJet template and use SendTemplatedEmailAsync.");
}
```

## Sending Templated Emails (Required)
`SendTemplatedEmailAsync` is the only supported send path. It looks up the configured template ID, attaches variables, and calls the MailJet API. Missing template IDs cause an immediate failure so the issue can be fixed in configuration rather than silently falling back to ad-hoc HTML.

```csharp
public async Task SendTemplatedEmailAsync(string email, EmailTemplate template, object? variables = null)
{
    var templateId = GetTemplateId(template);
    if (templateId == 0)
    {
        _logger.LogError("Template {Template} is not configured (ID: 0) for {Email}", template, email);
        throw new InvalidOperationException($"Template {template} is not configured. Configure the template ID before sending.");
    }

    var client = new MailjetClient(_options.ApiKey, _options.ApiSecret);

    var messageBuilder = new TransactionalEmailBuilder()
        .WithFrom(new SendContact(_options.FromEmail, _options.FromName))
        .WithTo(new SendContact(email))
        .WithTemplateLanguage(true)
        .WithTemplateId(templateId)
        .WithTemplateErrorReporting(new SendContact("ops@example.com")); // Update to your operations contact.

    if (variables != null)
    {
        var variableDictionary = ConvertVariablesToDictionary(variables);
        messageBuilder.WithVariables(variableDictionary);
    }

    var response = await client.SendTransactionalEmailAsync(messageBuilder.Build());
    // Logs the MailJet response status for troubleshooting.
}
```

Key behaviours:
- `GetTemplateId` maps the enum value to a configured template ID; `0` now throws with a clear error.
- `WithTemplateLanguage(true)` enables MailJet’s template rendering engine.
- `WithTemplateErrorReporting` should point to a monitored inbox so rendering issues surface quickly.
- Variables are optional and only included when provided.
- Response details are logged for auditing and debugging.

## Working with Template Variables
`ConvertVariablesToDictionary` reflects over the anonymous or POCO payload you pass into `SendTemplatedEmailAsync` and converts it to the key/value dictionary expected by MailJet:

```csharp
private static Dictionary<string, object> ConvertVariablesToDictionary(object variables)
{
    var dictionary = new Dictionary<string, object>();

    foreach (var property in variables.GetType().GetProperties())
    {
        var value = property.GetValue(variables);
        if (value != null)
        {
            dictionary[property.Name] = value;
        }
    }

    dictionary.Add("submittedAt", DateTime.UtcNow.ToLongDateString());
    return dictionary;
}
```

Notes:
- Property names are case-sensitive and must match the placeholder names defined in the MailJet template.
- The helper automatically adds a `submittedAt` variable (UTC date in long format). Adjust or extend this helper if your project requires different defaults.

## Usage Examples
Inject `ITemplatedEmailSender` into your controller, page model, or background job and call it with the relevant template:

```csharp
public class AccountController
{
    private readonly ITemplatedEmailSender _emailSender;

    public AccountController(ITemplatedEmailSender emailSender)
    {
        _emailSender = emailSender;
    }

    public async Task SendPasswordResetAsync(string email, string resetUrl)
    {
        await _emailSender.SendTemplatedEmailAsync(
            email,
            EmailTemplate.PasswordReset,
            new { resetUrl }
        );
    }
}
```

## Adding or Updating Templates
1. Create or update the transactional template inside MailJet and capture its numeric ID.
2. Extend the `EmailTemplate` enum and `MailJetTemplates` class if you are introducing a new template type.
3. Add the template ID to configuration for each environment (local, staging, production, etc.).
4. Update calling code to use the new enum value and pass any required template variables.
5. Communicate variable expectations to the team responsible for maintaining templates so placeholders stay in sync.

## Troubleshooting Tips
- **Empty `response.Messages`**: MailJet sometimes returns an empty array even when the call succeeds. Check your logs and confirm delivery in the MailJet dashboard.
- **Template ID is 0**: Indicates the template is missing or misnamed in configuration. Update `MailJet:Templates` and redeploy.
- **Invalid template variables**: Ensure that property names in the variables object exactly match the placeholders defined in the MailJet template.
- **Template rendering errors**: MailJet sends template parsing issues to the address specified in `WithTemplateErrorReporting`. Route this to an inbox your team monitors.

By keeping most email flows on the templated path, you minimise HTML maintenance in code and make it easier for non-engineers to update customer-facing content across projects.
