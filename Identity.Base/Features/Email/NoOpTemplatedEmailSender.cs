using System.Threading;
using System.Text.Json;
using Identity.Base.Logging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Identity.Base.Features.Email;

internal sealed class NoOpTemplatedEmailSender : ITemplatedEmailSender
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    private static int _warnedInProduction;

    private readonly IHostEnvironment _environment;
    private readonly ILogger<NoOpTemplatedEmailSender> _logger;
    private readonly ILogSanitizer _sanitizer;

    public NoOpTemplatedEmailSender(IHostEnvironment environment, ILogger<NoOpTemplatedEmailSender> logger, ILogSanitizer sanitizer)
    {
        _environment = environment;
        _logger = logger;
        _sanitizer = sanitizer;
    }

    public Task SendAsync(TemplatedEmail email, CancellationToken cancellationToken = default)
    {
        if (!_environment.IsDevelopment())
        {
            if (_environment.IsProduction() && Interlocked.Exchange(ref _warnedInProduction, 1) == 0)
            {
                _logger.LogWarning(
                    "[NoOpEmailSender] Active in Production. Email delivery is disabled. Recipient={Recipient}, TemplateKey={TemplateKey}",
                    _sanitizer.RedactEmail(email.ToEmail),
                    email.TemplateKey);
            }

            return Task.CompletedTask;
        }

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
