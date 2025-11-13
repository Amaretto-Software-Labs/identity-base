using System.Linq;
using Identity.Base.Features.Email;
using Identity.Base.Logging;
using Mailjet.Client;
using Mailjet.Client.TransactionalEmails;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Identity.Base.Email.MailJet;

internal sealed class MailJetEmailSender : ITemplatedEmailSender
{
    private readonly MailJetOptions _options;
    private readonly ILogger<MailJetEmailSender> _logger;
    private readonly ILogSanitizer _sanitizer;

    public MailJetEmailSender(
        IOptions<MailJetOptions> options,
        ILogger<MailJetEmailSender> logger,
        ILogSanitizer sanitizer)
    {
        _options = options.Value;
        _logger = logger;
        _sanitizer = sanitizer;
    }

    public async Task SendAsync(TemplatedEmail email, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            _logger.LogWarning("MailJet sender is disabled. Skipping email to {Email} (template {TemplateKey}).", _sanitizer.RedactEmail(email.ToEmail), email.TemplateKey);
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();

        var templateId = ResolveTemplateId(email.TemplateKey);
        if (templateId <= 0)
        {
            throw new InvalidOperationException($"MailJet template '{email.TemplateKey}' is not configured.");
        }

        var client = new MailjetClient(_options.ApiKey, _options.ApiSecret);
        var message = BuildTemplatedEmail(email, templateId);

        try
        {
            var response = await client.SendTransactionalEmailAsync(message, false, true);

            if (response.Messages is null || !response.Messages.Any())
            {
                _logger.LogWarning("MailJet returned an empty response for {Email}", _sanitizer.RedactEmail(email.ToEmail));
                return;
            }

            foreach (var result in response.Messages)
            {
                if (result.Errors is { } errors && errors.Any())
                {
                    var description = string.Join(", ", errors.Select(error => error.ErrorMessage));
                    _logger.LogError("MailJet reported errors for {Email}: {Errors}", _sanitizer.RedactEmail(email.ToEmail), description);
                    throw new InvalidOperationException($"MailJet send failed: {description}");
                }
            }

            _logger.LogInformation("MailJet email dispatched to {Email} using template {TemplateKey} (Id {TemplateId})", _sanitizer.RedactEmail(email.ToEmail), email.TemplateKey, templateId);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(ex, "MailJet send failed for {Email}", _sanitizer.RedactEmail(email.ToEmail));
            throw;
        }
    }

    private TransactionalEmail BuildTemplatedEmail(TemplatedEmail email, long templateId)
    {
        var builder = new TransactionalEmailBuilder()
            .WithFrom(new SendContact(_options.FromEmail, _options.FromName))
            .WithSubject(email.Subject ?? string.Empty)
            .WithTo(new SendContact(email.ToEmail, email.ToName))
            .WithTemplateId(templateId)
            .WithTemplateLanguage(true)
            .WithVariables(email.Variables);

        if (_options.ErrorReporting is { Enabled: true } reporting && !string.IsNullOrWhiteSpace(reporting.Email))
        {
            builder.WithTemplateErrorReporting(new SendContact(reporting.Email, reporting.Email));
        }

        return builder.Build();
    }

    private long ResolveTemplateId(string templateKey)
    {
        long templateId = templateKey switch
        {
            TemplatedEmailKeys.AccountConfirmation => _options.Templates.Confirmation,
            TemplatedEmailKeys.PasswordReset => _options.Templates.PasswordReset,
            TemplatedEmailKeys.EmailMfaChallenge => _options.Templates.MfaChallenge,
            _ => 0
        };

        if (templateId == 0 && !long.TryParse(templateKey, out templateId))
        {
            _logger.LogWarning("Template key '{TemplateKey}' is either not configured or cannot be converted to long!", templateKey);
        }

        return templateId;
    }
}
