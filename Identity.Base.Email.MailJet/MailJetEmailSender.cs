using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Identity.Base.Features.Email;
using Identity.Base.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Identity.Base.Email.MailJet;

internal sealed class MailJetEmailSender : ITemplatedEmailSender
{
    internal const string HttpClientName = "Identity.Base.Email.MailJet";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly MailJetOptions _options;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<MailJetEmailSender> _logger;
    private readonly ILogSanitizer _sanitizer;

    public MailJetEmailSender(
        IOptions<MailJetOptions> options,
        IHttpClientFactory httpClientFactory,
        ILogger<MailJetEmailSender> logger,
        ILogSanitizer sanitizer)
    {
        _options = options.Value;
        _httpClientFactory = httpClientFactory;
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

        var client = _httpClientFactory.CreateClient(HttpClientName);

        try
        {
            var payload = BuildRequestPayload(email, templateId);

            using var request = new HttpRequestMessage(HttpMethod.Post, "v3.1/send")
            {
                Content = JsonContent.Create(payload, options: JsonOptions)
            };
            request.Headers.Authorization = CreateBasicAuth(_options.ApiKey, _options.ApiSecret);

            using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "MailJet send failed for {Email}. StatusCode={StatusCode}",
                    _sanitizer.RedactEmail(email.ToEmail),
                    (int)response.StatusCode);
                throw new InvalidOperationException("MailJet send failed.");
            }

            if (string.IsNullOrWhiteSpace(responseBody))
            {
                _logger.LogWarning("MailJet returned an empty response for {Email}", _sanitizer.RedactEmail(email.ToEmail));
                return;
            }

            if (TryExtractMailJetErrors(responseBody, out var description))
            {
                _logger.LogError("MailJet reported errors for {Email}: {Errors}", _sanitizer.RedactEmail(email.ToEmail), description);
                throw new InvalidOperationException($"MailJet send failed: {description}");
            }

            _logger.LogInformation(
                "MailJet email dispatched to {Email} using template {TemplateKey} (Id {TemplateId})",
                _sanitizer.RedactEmail(email.ToEmail),
                email.TemplateKey,
                templateId);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(ex, "MailJet send failed for {Email}", _sanitizer.RedactEmail(email.ToEmail));
            throw;
        }
    }

    private MailJetSendRequest BuildRequestPayload(TemplatedEmail email, long templateId)
    {
        var message = new MailJetMessage
        {
            From = new MailJetContact { Email = _options.FromEmail, Name = _options.FromName },
            To = new[] { new MailJetContact { Email = email.ToEmail, Name = email.ToName } },
            Subject = email.Subject ?? string.Empty,
            TemplateId = templateId,
            TemplateLanguage = true,
            Variables = email.Variables
        };

        if (_options.ErrorReporting is { Enabled: true } reporting && !string.IsNullOrWhiteSpace(reporting.Email))
        {
            message.TemplateErrorReporting = new MailJetContact { Email = reporting.Email, Name = reporting.Email };
        }

        return new MailJetSendRequest
        {
            Messages = new[] { message }
        };
    }

    private static AuthenticationHeaderValue CreateBasicAuth(string apiKey, string apiSecret)
    {
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{apiKey}:{apiSecret}"));
        return new AuthenticationHeaderValue("Basic", credentials);
    }

    private static bool TryExtractMailJetErrors(string responseBody, out string description)
    {
        description = string.Empty;

        try
        {
            using var document = JsonDocument.Parse(responseBody);
            if (!document.RootElement.TryGetProperty("Messages", out var messages) || messages.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            var errors = messages
                .EnumerateArray()
                .SelectMany(message => message.TryGetProperty("Errors", out var messageErrors) && messageErrors.ValueKind == JsonValueKind.Array
                    ? messageErrors.EnumerateArray()
                    : Enumerable.Empty<JsonElement>())
                .Select(error => error.TryGetProperty("ErrorMessage", out var value) ? value.GetString() : null)
                .Where(message => !string.IsNullOrWhiteSpace(message))
                .ToList();

            if (errors.Count == 0)
            {
                return false;
            }

            description = string.Join(", ", errors);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
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

    private sealed class MailJetSendRequest
    {
        [JsonPropertyName("Messages")]
        public MailJetMessage[] Messages { get; init; } = Array.Empty<MailJetMessage>();
    }

    private sealed class MailJetMessage
    {
        [JsonPropertyName("From")]
        public MailJetContact From { get; init; } = new();

        [JsonPropertyName("To")]
        public MailJetContact[] To { get; init; } = Array.Empty<MailJetContact>();

        [JsonPropertyName("Subject")]
        public string Subject { get; init; } = string.Empty;

        [JsonPropertyName("TemplateID")]
        public long TemplateId { get; init; }

        [JsonPropertyName("TemplateLanguage")]
        public bool TemplateLanguage { get; init; }

        [JsonPropertyName("Variables")]
        public IDictionary<string, object?> Variables { get; init; } = new Dictionary<string, object?>();

        [JsonPropertyName("TemplateErrorReporting")]
        public MailJetContact? TemplateErrorReporting { get; set; }
    }

    private sealed class MailJetContact
    {
        [JsonPropertyName("Email")]
        public string Email { get; init; } = string.Empty;

        [JsonPropertyName("Name")]
        public string? Name { get; init; }
    }
}
