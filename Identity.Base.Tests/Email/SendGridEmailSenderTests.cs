using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Identity.Base.Email.SendGrid;
using Identity.Base.Features.Email;
using Identity.Base.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shouldly;

namespace Identity.Base.Tests.Email;

public sealed class SendGridEmailSenderTests
{
    [Fact]
    public void AddSendGridEmailSender_throws_options_validation_exception_when_enabled_and_incomplete()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<ILogSanitizer>(new PassthroughLogSanitizer());

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SendGrid:Enabled"] = "true",
                ["SendGrid:ApiKey"] = "SG.test",
                ["SendGrid:FromEmail"] = "noreply@example.com",
                ["SendGrid:Templates:Confirmation"] = "", // missing
                ["SendGrid:Templates:PasswordReset"] = "d-reset",
                ["SendGrid:Templates:MfaChallenge"] = "d-mfa",
            })
            .Build();

        services.AddSendGridEmailSender(configuration);

        using var provider = services.BuildServiceProvider();
        Should.Throw<OptionsValidationException>(() => provider.GetRequiredService<ITemplatedEmailSender>());
    }

    [Fact]
    public async Task SendAsync_uses_template_key_as_template_id_for_unknown_keys()
    {
        var handler = new CapturingHandler((request, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.Accepted) { Content = new StringContent(string.Empty) }));

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<ILogSanitizer>(new PassthroughLogSanitizer());

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SendGrid:Enabled"] = "true",
                ["SendGrid:ApiKey"] = "SG.test",
                ["SendGrid:FromEmail"] = "noreply@example.com",
                ["SendGrid:Templates:Confirmation"] = "d-confirm",
                ["SendGrid:Templates:PasswordReset"] = "d-reset",
                ["SendGrid:Templates:MfaChallenge"] = "d-mfa",
            })
            .Build();

        services.AddSendGridEmailSender(configuration);
        services.AddHttpClient("Identity.Base.Email.SendGrid")
            .ConfigurePrimaryHttpMessageHandler(() => handler);

        await using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ITemplatedEmailSender>();

        await sender.SendAsync(new TemplatedEmail(
            TemplateKey: "d-unknown-template",
            ToEmail: "alice@example.com",
            ToName: "Alice",
            Variables: new Dictionary<string, object?> { ["k"] = "v" },
            Subject: "Hello"));

        handler.Requests.Count.ShouldBe(1);
        var captured = handler.Requests.Single();
        captured.RequestUri.ShouldNotBeNull();
        captured.RequestUri!.ToString().ShouldContain("v3/mail/send");
        captured.AuthorizationScheme.ShouldBe("Bearer");
        captured.AuthorizationParameter.ShouldBe("SG.test");

        using var doc = JsonDocument.Parse(captured.Body ?? string.Empty);
        doc.RootElement.GetProperty("template_id").GetString().ShouldBe("d-unknown-template");
        doc.RootElement.GetProperty("from").GetProperty("email").GetString().ShouldBe("noreply@example.com");
        doc.RootElement.GetProperty("personalizations")[0].GetProperty("to")[0].GetProperty("email").GetString().ShouldBe("alice@example.com");
        doc.RootElement.GetProperty("personalizations")[0].GetProperty("dynamic_template_data").GetProperty("k").GetString().ShouldBe("v");
    }

    [Fact]
    public async Task SendAsync_throws_with_parsed_errors_when_sendgrid_returns_error_payload()
    {
        const string errorJson = """{"errors":[{"message":"bad request"},{"message":"invalid template"}]}""";
        var handler = new CapturingHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = new StringContent(errorJson) }));

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<ILogSanitizer>(new PassthroughLogSanitizer());

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SendGrid:Enabled"] = "true",
                ["SendGrid:ApiKey"] = "SG.test",
                ["SendGrid:FromEmail"] = "noreply@example.com",
                ["SendGrid:Templates:Confirmation"] = "d-confirm",
                ["SendGrid:Templates:PasswordReset"] = "d-reset",
                ["SendGrid:Templates:MfaChallenge"] = "d-mfa",
            })
            .Build();

        services.AddSendGridEmailSender(configuration);
        services.AddHttpClient("Identity.Base.Email.SendGrid")
            .ConfigurePrimaryHttpMessageHandler(() => handler);

        await using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ITemplatedEmailSender>();

        var ex = await Should.ThrowAsync<InvalidOperationException>(() => sender.SendAsync(new TemplatedEmail(
            TemplateKey: TemplatedEmailKeys.PasswordReset,
            ToEmail: "alice@example.com",
            ToName: "Alice",
            Variables: new Dictionary<string, object?> { ["resetUrl"] = "https://example.com/reset" },
            Subject: "Reset")));

        ex.Message.ShouldContain("SendGrid send failed:");
        ex.Message.ShouldContain("bad request");
        ex.Message.ShouldContain("invalid template");
    }

    [Fact]
    public async Task SendAsync_does_not_throw_when_success_response_contains_warnings()
    {
        const string warningJson = """{"errors":[{"message":"suppressed warning"}]}""";
        var handler = new CapturingHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.Accepted) { Content = new StringContent(warningJson) }));

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<ILogSanitizer>(new PassthroughLogSanitizer());

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SendGrid:Enabled"] = "true",
                ["SendGrid:ApiKey"] = "SG.test",
                ["SendGrid:FromEmail"] = "noreply@example.com",
                ["SendGrid:Templates:Confirmation"] = "d-confirm",
                ["SendGrid:Templates:PasswordReset"] = "d-reset",
                ["SendGrid:Templates:MfaChallenge"] = "d-mfa",
            })
            .Build();

        services.AddSendGridEmailSender(configuration);
        services.AddHttpClient("Identity.Base.Email.SendGrid")
            .ConfigurePrimaryHttpMessageHandler(() => handler);

        await using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ITemplatedEmailSender>();

        await sender.SendAsync(new TemplatedEmail(
            TemplateKey: TemplatedEmailKeys.EmailMfaChallenge,
            ToEmail: "alice@example.com",
            ToName: "Alice",
            Variables: new Dictionary<string, object?> { ["code"] = "123456" },
            Subject: "MFA"));
    }

    [Fact]
    public void SendGridOptionsValidator_detects_missing_values_when_enabled()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<ILogSanitizer>(new PassthroughLogSanitizer());

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SendGrid:Enabled"] = "false",
            })
            .Build();

        services.AddSendGridEmailSender(configuration);
        using var provider = services.BuildServiceProvider();

        var validator = provider.GetServices<IValidateOptions<SendGridOptions>>()
            .Single(service => service.GetType().Name == "SendGridOptionsValidator");

        validator.Validate(null, new SendGridOptions { Enabled = false }).Succeeded.ShouldBeTrue();

        var result = validator.Validate(null, new SendGridOptions
        {
            Enabled = true,
            ApiKey = "",
            FromEmail = "",
            Templates = new SendGridTemplateOptions()
        });

        result.Succeeded.ShouldBeFalse();
        result.FailureMessage.ShouldNotBeNull();
        result.FailureMessage.ShouldContain("ApiKey");
        result.FailureMessage.ShouldContain("FromEmail");
        result.FailureMessage.ShouldContain("Templates.Confirmation");
    }

    [Fact]
    public async Task SendGrid_health_check_reports_disabled_incomplete_and_ok()
    {
        static async Task<HealthCheckResult> RunAsync(SendGridOptions options)
        {
            var healthCheckType = typeof(SendGridOptions).Assembly.GetType("Identity.Base.Email.SendGrid.SendGridOptionsHealthCheck", throwOnError: true);
            var healthCheck = (IHealthCheck)Activator.CreateInstance(healthCheckType!, Microsoft.Extensions.Options.Options.Create(options))!;
            return await healthCheck.CheckHealthAsync(new HealthCheckContext());
        }

        var disabled = await RunAsync(new SendGridOptions { Enabled = false });
        disabled.Status.ShouldBe(HealthStatus.Healthy);
        disabled.Description.ShouldBe("SendGrid disabled.");

        var incomplete = await RunAsync(new SendGridOptions { Enabled = true, ApiKey = "", FromEmail = "" });
        incomplete.Status.ShouldBe(HealthStatus.Degraded);
        incomplete.Description.ShouldBe("SendGrid configuration is incomplete.");

        var templatesMissing = await RunAsync(new SendGridOptions
        {
            Enabled = true,
            ApiKey = "SG.test",
            FromEmail = "noreply@example.com",
            Templates = new SendGridTemplateOptions
            {
                Confirmation = "",
                PasswordReset = "",
                MfaChallenge = ""
            }
        });
        templatesMissing.Status.ShouldBe(HealthStatus.Degraded);
        templatesMissing.Description.ShouldBe("SendGrid templates missing.");

        var ok = await RunAsync(new SendGridOptions
        {
            Enabled = true,
            ApiKey = "SG.test",
            FromEmail = "noreply@example.com",
            Templates = new SendGridTemplateOptions
            {
                Confirmation = "d-confirm",
                PasswordReset = "d-reset",
                MfaChallenge = "d-mfa"
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
