using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Identity.Base.Features.Email;
using Identity.Base.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Identity.Base.Email.SendGrid;

internal sealed class SendGridEmailSender(
    IOptions<SendGridOptions> options,
    IHttpClientFactory httpClientFactory,
    ILogger<SendGridEmailSender> logger,
    ILogSanitizer sanitizer) : ITemplatedEmailSender
{
    internal const string HttpClientName = "Identity.Base.Email.SendGrid";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly SendGridOptions _options = options.Value;

    public async Task SendAsync(TemplatedEmail email, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            logger.LogWarning(
                "SendGrid sender is disabled. Skipping email to {Email} (template {TemplateKey}).",
                sanitizer.RedactEmail(email.ToEmail),
                email.TemplateKey);
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();

        var templateId = ResolveTemplateId(email.TemplateKey);
        if (string.IsNullOrWhiteSpace(templateId))
        {
            throw new InvalidOperationException($"SendGrid template '{email.TemplateKey}' is not configured.");
        }

        var client = httpClientFactory.CreateClient(HttpClientName);

        try
        {
            var payload = BuildRequestPayload(email, templateId);

            using var request = new HttpRequestMessage(HttpMethod.Post, "v3/mail/send");
            request.Content = JsonContent.Create(payload, options: JsonOptions);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);

            using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogError(
                    "SendGrid send failed for {Email}. StatusCode={StatusCode}",
                    sanitizer.RedactEmail(email.ToEmail),
                    (int)response.StatusCode);

                if (!string.IsNullOrWhiteSpace(responseBody) && TryExtractSendGridErrors(responseBody, out var description))
                {
                    logger.LogError("SendGrid reported errors for {Email}: {Errors}", sanitizer.RedactEmail(email.ToEmail), description);
                    throw new InvalidOperationException($"SendGrid send failed: {description}");
                }

                throw new InvalidOperationException("SendGrid send failed.");
            }

            if (!string.IsNullOrWhiteSpace(responseBody) && TryExtractSendGridErrors(responseBody, out var warningDescription))
            {
                logger.LogWarning("SendGrid returned warnings for {Email}: {Warnings}", sanitizer.RedactEmail(email.ToEmail), warningDescription);
            }

            logger.LogInformation(
                "SendGrid email dispatched to {Email} using template {TemplateKey} (Id {TemplateId})",
                sanitizer.RedactEmail(email.ToEmail),
                email.TemplateKey,
                templateId);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogError(ex, "SendGrid send failed for {Email}", sanitizer.RedactEmail(email.ToEmail));
            throw;
        }
    }

    private SendGridSendRequest BuildRequestPayload(TemplatedEmail email, string templateId)
    {
        var personalization = new SendGridPersonalization
        {
            To = [new SendGridContact { Email = email.ToEmail, Name = email.ToName }],
            DynamicTemplateData = email.Variables
        };

        return new SendGridSendRequest
        {
            From = new SendGridContact { Email = _options.FromEmail, Name = _options.FromName },
            TemplateId = templateId,
            Subject = email.Subject,
            Personalizations = [personalization]
        };
    }

    private static bool TryExtractSendGridErrors(string responseBody, out string description)
    {
        description = string.Empty;

        try
        {
            using var document = JsonDocument.Parse(responseBody);
            if (!document.RootElement.TryGetProperty("errors", out var errors) || errors.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            var messages = errors
                .EnumerateArray()
                .Select(error => error.TryGetProperty("message", out var message) ? message.GetString() : null)
                .Where(message => !string.IsNullOrWhiteSpace(message))
                .ToList();

            if (messages.Count == 0)
            {
                return false;
            }

            description = string.Join(", ", messages);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private string ResolveTemplateId(string templateKey)
    {
        var isKnownKey = templateKey is TemplatedEmailKeys.AccountConfirmation
            or TemplatedEmailKeys.PasswordReset
            or TemplatedEmailKeys.EmailMfaChallenge;

        var templateId = templateKey switch
        {
            TemplatedEmailKeys.AccountConfirmation => _options.Templates.Confirmation,
            TemplatedEmailKeys.PasswordReset => _options.Templates.PasswordReset,
            TemplatedEmailKeys.EmailMfaChallenge => _options.Templates.MfaChallenge,
            _ => string.Empty
        };

        if (string.IsNullOrWhiteSpace(templateId) && !isKnownKey)
        {
            templateId = templateKey;
        }

        return templateId;
    }

    private sealed class SendGridSendRequest
    {
        [JsonPropertyName("personalizations")]
        public SendGridPersonalization[] Personalizations { get; init; } = [];

        [JsonPropertyName("from")]
        public SendGridContact From { get; init; } = new();

        [JsonPropertyName("template_id")]
        public string TemplateId { get; init; } = string.Empty;

        [JsonPropertyName("subject")]
        public string? Subject { get; init; }
    }

    private sealed class SendGridPersonalization
    {
        [JsonPropertyName("to")]
        public SendGridContact[] To { get; init; } = [];

        [JsonPropertyName("dynamic_template_data")]
        public IDictionary<string, object?> DynamicTemplateData { get; init; } = new Dictionary<string, object?>();
    }

    private sealed class SendGridContact
    {
        [JsonPropertyName("email")]
        public string Email { get; init; } = string.Empty;

        [JsonPropertyName("name")]
        public string? Name { get; init; }
    }
}
