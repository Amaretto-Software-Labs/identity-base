using System.Linq;
using Identity.Base.Options;
using Mailjet.Client;
using Mailjet.Client.TransactionalEmails;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Identity.Base.Features.Email;

public sealed class MailJetEmailSender : ITemplatedEmailSender
{
    private readonly MailJetOptions _options;
    private readonly ILogger<MailJetEmailSender> _logger;

    public MailJetEmailSender(IOptions<MailJetOptions> options, ILogger<MailJetEmailSender> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendAsync(TemplatedEmail email, CancellationToken cancellationToken = default)
    {
        if (email.TemplateId <= 0)
        {
            throw new InvalidOperationException("MailJet templated emails require a positive template id.");
        }

        cancellationToken.ThrowIfCancellationRequested();

        var client = CreateClient();

        var message = BuildTemplatedEmail(email);

        try
        {
            var response = await client.SendTransactionalEmailAsync(message, false, true);

            if (response.Messages is null || !response.Messages.Any())
            {
                _logger.LogWarning("MailJet returned an empty response for {Email}", email.ToEmail);
                return;
            }

            foreach (var result in response.Messages)
            {
                if (result.Errors is { } errors && errors.Any())
                {
                    var description = string.Join(", ", errors.Select(error => error.ErrorMessage));
                    _logger.LogError("MailJet reported errors for {Email}: {Errors}", email.ToEmail, description);
                    throw new InvalidOperationException($"MailJet send failed: {description}");
                }
            }

            _logger.LogInformation("MailJet email dispatched to {Email} using template {TemplateId}", email.ToEmail, email.TemplateId);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(ex, "MailJet send failed for {Email}", email.ToEmail);
            throw;
        }
    }

    private MailjetClient CreateClient() => new(_options.ApiKey, _options.ApiSecret);

    private TransactionalEmail BuildTemplatedEmail(TemplatedEmail email)
    {
        var builder = new TransactionalEmailBuilder()
            .WithFrom(new SendContact(_options.FromEmail, _options.FromName))
            .WithSubject(email.Subject ?? string.Empty)
            .WithTo(new SendContact(email.ToEmail, email.ToName))
            .WithTemplateId(email.TemplateId)
            .WithTemplateLanguage(true)
            .WithVariables(email.Variables);

        if (_options.ErrorReporting is { Enabled: true } reporting && !string.IsNullOrWhiteSpace(reporting.Email))
        {
            builder.WithTemplateErrorReporting(new SendContact(reporting.Email, reporting.Email));
        }

        return builder.Build();
    }
}
