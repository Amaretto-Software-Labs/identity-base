using System.Collections.Generic;
using System.Net.Http.Headers;
using System.Text;
using Identity.Base.Identity;
using Identity.Base.Logging;
using Identity.Base.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Identity.Base.Features.Authentication.Mfa;

public sealed class TwilioMfaChallengeSender : IMfaChallengeSender
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly SmsChallengeOptions _options;
    private readonly ILogger<TwilioMfaChallengeSender> _logger;
    private readonly ILogSanitizer _sanitizer;

    public string Method => "sms";

    public TwilioMfaChallengeSender(
        IHttpClientFactory httpClientFactory,
        IOptions<MfaOptions> options,
        ILogger<TwilioMfaChallengeSender> logger,
        ILogSanitizer sanitizer)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value.Sms;
        _logger = logger;
        _sanitizer = sanitizer;
    }

    public async Task SendChallengeAsync(ApplicationUser user, string code, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            throw new InvalidOperationException("SMS MFA challenge is disabled.");
        }

        if (string.IsNullOrWhiteSpace(user.PhoneNumber) || !user.PhoneNumberConfirmed)
        {
            throw new InvalidOperationException("User does not have a confirmed phone number.");
        }

        var body = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["To"] = user.PhoneNumber,
            ["From"] = _options.FromPhoneNumber,
            ["Body"] = $"Your verification code is {code}."
        });

        var request = new HttpRequestMessage(HttpMethod.Post, $"https://api.twilio.com/2010-04-01/Accounts/{_options.AccountSid}/Messages.json")
        {
            Content = body
        };

        var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{_options.AccountSid}:{_options.AuthToken}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);

        var client = _httpClientFactory.CreateClient("TwilioSms");
        var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var payload = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Twilio SMS send failed with status {Status} for {Recipient}", response.StatusCode, _sanitizer.RedactPhoneNumber(user.PhoneNumber));
            _logger.LogDebug("Twilio response payload: {Payload}", payload);
            throw new InvalidOperationException("Failed to send SMS challenge.");
        }

        _logger.LogInformation("Sent SMS MFA challenge to {PhoneNumber}", _sanitizer.RedactPhoneNumber(user.PhoneNumber));
    }
}
