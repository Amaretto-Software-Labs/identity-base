using System.Text.Json;
using Identity.Base.Logging;
using Microsoft.Extensions.Logging;

namespace Identity.Base.Features.Email;

internal sealed class NoOpTemplatedEmailSender : ITemplatedEmailSender
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    private readonly ILogger<NoOpTemplatedEmailSender> _logger;
    private readonly ILogSanitizer _sanitizer;

    public NoOpTemplatedEmailSender(ILogger<NoOpTemplatedEmailSender> logger, ILogSanitizer sanitizer)
    {
        _logger = logger;
        _sanitizer = sanitizer;
    }

    public Task SendAsync(TemplatedEmail email, CancellationToken cancellationToken = default)
    {
        var payload = JsonSerializer.Serialize(email.Variables, SerializerOptions);
        _logger.LogInformation(
            "[NoOpEmailSender] Skipping outbound email. Recipient={Recipient}, TemplateKey={TemplateKey}, Subject={Subject}, Variables={Variables}",
            _sanitizer.RedactEmail(email.ToEmail),
            email.TemplateKey,
            email.Subject ?? string.Empty,
            payload);

        return Task.CompletedTask;
    }
}
