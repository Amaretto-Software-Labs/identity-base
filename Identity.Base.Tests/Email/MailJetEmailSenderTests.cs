using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Identity.Base.Email.MailJet;
using Identity.Base.Features.Email;
using Identity.Base.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shouldly;

namespace Identity.Base.Tests.Email;

public sealed class MailJetEmailSenderTests
{
    [Fact]
    public async Task SendAsync_uses_numeric_template_key_when_not_configured()
    {
        var handler = new CapturingHandler((request, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("""{"Messages":[]}""") }));

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<ILogSanitizer>(new PassthroughLogSanitizer());

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MailJet:Enabled"] = "true",
                ["MailJet:ApiKey"] = "test-key",
                ["MailJet:ApiSecret"] = "test-secret",
                ["MailJet:FromEmail"] = "noreply@example.com",
                ["MailJet:Templates:Confirmation"] = "1",
                ["MailJet:Templates:PasswordReset"] = "2",
                ["MailJet:Templates:MfaChallenge"] = "3",
            })
            .Build();

        services.AddMailJetEmailSender(configuration);
        services.AddHttpClient("Identity.Base.Email.MailJet")
            .ConfigurePrimaryHttpMessageHandler(() => handler);

        await using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ITemplatedEmailSender>();

        await sender.SendAsync(new TemplatedEmail(
            TemplateKey: "12345",
            ToEmail: "alice@example.com",
            ToName: "Alice",
            Variables: new Dictionary<string, object?> { ["k"] = "v" },
            Subject: "Hello"));

        handler.Requests.Count.ShouldBe(1);
        var captured = handler.Requests.Single();
        captured.AuthorizationScheme.ShouldBe("Basic");
        captured.RequestUri.ShouldNotBeNull();
        captured.RequestUri!.ToString().ShouldContain("v3.1/send");

        using var doc = JsonDocument.Parse(captured.Body ?? string.Empty);
        var message = doc.RootElement.GetProperty("Messages")[0];
        message.GetProperty("TemplateID").GetInt64().ShouldBe(12345);
        message.GetProperty("From").GetProperty("Email").GetString().ShouldBe("noreply@example.com");
        message.GetProperty("To")[0].GetProperty("Email").GetString().ShouldBe("alice@example.com");
        message.GetProperty("Variables").GetProperty("k").GetString().ShouldBe("v");
    }

    [Fact]
    public async Task SendAsync_throws_on_non_success_status()
    {
        var handler = new CapturingHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = new StringContent("bad") }));

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<ILogSanitizer>(new PassthroughLogSanitizer());

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MailJet:Enabled"] = "true",
                ["MailJet:ApiKey"] = "test-key",
                ["MailJet:ApiSecret"] = "test-secret",
                ["MailJet:FromEmail"] = "noreply@example.com",
                ["MailJet:Templates:Confirmation"] = "1",
                ["MailJet:Templates:PasswordReset"] = "2",
                ["MailJet:Templates:MfaChallenge"] = "3",
            })
            .Build();

        services.AddMailJetEmailSender(configuration);
        services.AddHttpClient("Identity.Base.Email.MailJet")
            .ConfigurePrimaryHttpMessageHandler(() => handler);

        await using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ITemplatedEmailSender>();

        var ex = await Should.ThrowAsync<InvalidOperationException>(() => sender.SendAsync(new TemplatedEmail(
            TemplateKey: TemplatedEmailKeys.PasswordReset,
            ToEmail: "alice@example.com",
            ToName: "Alice",
            Variables: new Dictionary<string, object?> { ["resetUrl"] = "https://example.com/reset" },
            Subject: "Reset")));

        ex.Message.ShouldContain("MailJet send failed");
    }

    [Fact]
    public async Task SendAsync_returns_without_throwing_when_response_body_is_empty()
    {
        var handler = new CapturingHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(string.Empty) }));

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<ILogSanitizer>(new PassthroughLogSanitizer());

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MailJet:Enabled"] = "true",
                ["MailJet:ApiKey"] = "test-key",
                ["MailJet:ApiSecret"] = "test-secret",
                ["MailJet:FromEmail"] = "noreply@example.com",
                ["MailJet:Templates:Confirmation"] = "1",
                ["MailJet:Templates:PasswordReset"] = "2",
                ["MailJet:Templates:MfaChallenge"] = "3",
            })
            .Build();

        services.AddMailJetEmailSender(configuration);
        services.AddHttpClient("Identity.Base.Email.MailJet")
            .ConfigurePrimaryHttpMessageHandler(() => handler);

        await using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ITemplatedEmailSender>();

        await sender.SendAsync(new TemplatedEmail(
            TemplateKey: TemplatedEmailKeys.AccountConfirmation,
            ToEmail: "alice@example.com",
            ToName: "Alice",
            Variables: new Dictionary<string, object?> { ["confirmationUrl"] = "https://example.com/confirm" },
            Subject: "Welcome"));
    }

    [Fact]
    public async Task SendAsync_throws_with_parsed_errors_when_mailjet_returns_error_payload()
    {
        const string errorJson = """{"Messages":[{"Errors":[{"ErrorMessage":"bad template"},{"ErrorMessage":"bad recipient"}]}]}""";
        var handler = new CapturingHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(errorJson) }));

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<ILogSanitizer>(new PassthroughLogSanitizer());

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MailJet:Enabled"] = "true",
                ["MailJet:ApiKey"] = "test-key",
                ["MailJet:ApiSecret"] = "test-secret",
                ["MailJet:FromEmail"] = "noreply@example.com",
                ["MailJet:Templates:Confirmation"] = "1",
                ["MailJet:Templates:PasswordReset"] = "2",
                ["MailJet:Templates:MfaChallenge"] = "3",
            })
            .Build();

        services.AddMailJetEmailSender(configuration);
        services.AddHttpClient("Identity.Base.Email.MailJet")
            .ConfigurePrimaryHttpMessageHandler(() => handler);

        await using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ITemplatedEmailSender>();

        var ex = await Should.ThrowAsync<InvalidOperationException>(() => sender.SendAsync(new TemplatedEmail(
            TemplateKey: TemplatedEmailKeys.AccountConfirmation,
            ToEmail: "alice@example.com",
            ToName: "Alice",
            Variables: new Dictionary<string, object?>(),
            Subject: "Welcome")));

        ex.Message.ShouldContain("MailJet send failed:");
        ex.Message.ShouldContain("bad template");
        ex.Message.ShouldContain("bad recipient");
    }

    [Fact]
    public async Task SendAsync_throws_when_template_key_is_unknown_and_not_numeric()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<ILogSanitizer>(new PassthroughLogSanitizer());

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MailJet:Enabled"] = "true",
                ["MailJet:ApiKey"] = "test-key",
                ["MailJet:ApiSecret"] = "test-secret",
                ["MailJet:FromEmail"] = "noreply@example.com",
                ["MailJet:Templates:Confirmation"] = "1",
                ["MailJet:Templates:PasswordReset"] = "2",
                ["MailJet:Templates:MfaChallenge"] = "3",
            })
            .Build();

        services.AddMailJetEmailSender(configuration);
        await using var provider = services.BuildServiceProvider();

        var sender = provider.GetRequiredService<ITemplatedEmailSender>();
        var ex = await Should.ThrowAsync<InvalidOperationException>(() => sender.SendAsync(new TemplatedEmail(
            TemplateKey: "unknown-template",
            ToEmail: "alice@example.com",
            ToName: "Alice",
            Variables: new Dictionary<string, object?>(),
            Subject: "Hello")));

        ex.Message.ShouldContain("not configured");
    }

    [Fact]
    public void MailJetOptionsValidator_detects_missing_values_when_enabled()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<ILogSanitizer>(new PassthroughLogSanitizer());

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MailJet:Enabled"] = "false",
            })
            .Build();

        services.AddMailJetEmailSender(configuration);
        using var provider = services.BuildServiceProvider();

        var validator = provider.GetServices<IValidateOptions<MailJetOptions>>()
            .Single(service => service.GetType().Name == "MailJetOptionsValidator");

        validator.Validate(null, new MailJetOptions { Enabled = false }).Succeeded.ShouldBeTrue();

        var result = validator.Validate(null, new MailJetOptions
        {
            Enabled = true,
            ApiKey = "",
            ApiSecret = "",
            FromEmail = "",
            Templates = new MailJetTemplateOptions(),
            ErrorReporting = new MailJetErrorReportingOptions { Enabled = true, Email = "" }
        });

        result.Succeeded.ShouldBeFalse();
        result.FailureMessage.ShouldNotBeNull();
        result.FailureMessage.ShouldContain("ApiKey");
        result.FailureMessage.ShouldContain("ApiSecret");
        result.FailureMessage.ShouldContain("FromEmail");
        result.FailureMessage.ShouldContain("Templates.Confirmation");
        result.FailureMessage.ShouldContain("ErrorReporting.Email");
    }

    [Fact]
    public async Task MailJet_health_check_reports_disabled_incomplete_templates_and_ok()
    {
        static async Task<HealthCheckResult> RunAsync(MailJetOptions options)
        {
            var healthCheckType = typeof(MailJetOptions).Assembly.GetType("Identity.Base.Email.MailJet.MailJetOptionsHealthCheck", throwOnError: true);
            var healthCheck = (IHealthCheck)Activator.CreateInstance(healthCheckType!, Microsoft.Extensions.Options.Options.Create(options))!;
            return await healthCheck.CheckHealthAsync(new HealthCheckContext());
        }

        var disabled = await RunAsync(new MailJetOptions { Enabled = false });
        disabled.Status.ShouldBe(HealthStatus.Healthy);
        disabled.Description.ShouldBe("MailJet disabled.");

        var incomplete = await RunAsync(new MailJetOptions { Enabled = true, ApiKey = "", ApiSecret = "", FromEmail = "" });
        incomplete.Status.ShouldBe(HealthStatus.Degraded);
        incomplete.Description.ShouldBe("MailJet configuration is incomplete.");

        var templatesMissing = await RunAsync(new MailJetOptions
        {
            Enabled = true,
            ApiKey = "test-key",
            ApiSecret = "test-secret",
            FromEmail = "noreply@example.com",
            Templates = new MailJetTemplateOptions
            {
                Confirmation = 0,
                PasswordReset = 0,
                MfaChallenge = 0
            }
        });
        templatesMissing.Status.ShouldBe(HealthStatus.Degraded);
        templatesMissing.Description.ShouldBe("MailJet templates missing.");

        var ok = await RunAsync(new MailJetOptions
        {
            Enabled = true,
            ApiKey = "test-key",
            ApiSecret = "test-secret",
            FromEmail = "noreply@example.com",
            Templates = new MailJetTemplateOptions
            {
                Confirmation = 1,
                PasswordReset = 2,
                MfaChallenge = 3
            }
        });
        ok.Status.ShouldBe(HealthStatus.Healthy);
    }

    private sealed class PassthroughLogSanitizer : ILogSanitizer
    {
        public string? RedactEmail(string? value) => value;

        public string? RedactPhoneNumber(string? value) => value;

        public string RedactToken(string? value) => "[redacted]";
    }

    private sealed record CapturedRequest(string? Body, string? AuthorizationScheme, string? AuthorizationParameter, Uri? RequestUri);

    private sealed class CapturingHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _responder;

        public List<CapturedRequest> Requests { get; } = new();

        public CapturingHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder)
        {
            _responder = responder;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            var auth = request.Headers.Authorization;
            Requests.Add(new CapturedRequest(body, auth?.Scheme, auth?.Parameter, request.RequestUri));
            return await _responder(request, cancellationToken);
        }
    }
}
