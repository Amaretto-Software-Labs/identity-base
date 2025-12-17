using Identity.Base.Email.SendGrid;
using Identity.Base.Features.Email;
using Identity.Base.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shouldly;

namespace Identity.Base.Tests.Email;

public sealed class SendGridEmailSenderRegistrationTests
{
    [Fact]
    public async Task AddSendGridEmailSender_registers_sender_and_noops_when_disabled()
    {
        var services = new ServiceCollection();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SendGrid:Enabled"] = "false",
            })
            .Build();

        services.AddLogging(builder => builder.AddDebug().SetMinimumLevel(LogLevel.Debug));
        services.AddSingleton<ILogSanitizer>(new TestLogSanitizer());

        services.AddSendGridEmailSender(configuration);

        await using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ITemplatedEmailSender>();

        sender.GetType().Name.ShouldBe("SendGridEmailSender");

        await sender.SendAsync(new TemplatedEmail(
            TemplateKey: TemplatedEmailKeys.AccountConfirmation,
            ToEmail: "alice@example.com",
            ToName: "Alice",
            Variables: new Dictionary<string, object?> { ["confirmationUrl"] = "https://example.com/confirm" },
            Subject: "Welcome"));
    }

    private sealed class TestLogSanitizer : ILogSanitizer
    {
        public string? RedactEmail(string? value) => value;

        public string? RedactPhoneNumber(string? value) => value;

        public string RedactToken(string? value) => "[redacted]";
    }
}
