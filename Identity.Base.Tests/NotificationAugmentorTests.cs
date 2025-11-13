using System;
using System.Net;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Identity.Base.Features.Notifications;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Identity.Base.Tests;

public class NotificationAugmentorTests : IClassFixture<IdentityApiFactory>
{
    private readonly IdentityApiFactory _factory;

    public NotificationAugmentorTests(IdentityApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task EmailConfirmationAugmentor_CanModifyTemplateVariables()
    {
        var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.AddSingleton<TestConfirmationAugmentor>();
                services.AddScoped<INotificationContextAugmentor<EmailConfirmationNotificationContext>>(sp => sp.GetRequiredService<TestConfirmationAugmentor>());
            });
        });

        using var scope = factory.Services.CreateScope();
        var emailSender = scope.ServiceProvider.GetRequiredService<FakeEmailSender>();
        emailSender.Clear();

        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/auth/register", new
        {
            email = $"augmentor-{Guid.NewGuid():N}@example.com",
            password = "Passw0rd!Passw0rd!",
            metadata = new
            {
                displayName = "Augmentor User"
            }
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);

        emailSender.Sent.ShouldNotBeEmpty();
        var email = emailSender.Sent.Last();
        email.TemplateKey.ShouldBe(TestConfirmationAugmentor.CustomTemplateKey);
        email.Variables.ShouldContainKey("supportEmail");
        email.Variables["supportEmail"].ShouldBe("support@example.com");
        email.Subject.ShouldBe("Custom Subject");
    }

    private sealed class TestConfirmationAugmentor : INotificationContextAugmentor<EmailConfirmationNotificationContext>
    {
        public const string CustomTemplateKey = "custom-confirmation";

        public ValueTask<NotificationAugmentorResult> AugmentAsync(EmailConfirmationNotificationContext context, CancellationToken cancellationToken = default)
        {
            context.TemplateKey = CustomTemplateKey;
            context.Subject = "Custom Subject";
            context.Variables["supportEmail"] = "support@example.com";
            return ValueTask.FromResult(NotificationAugmentorResult.Continue());
        }
    }
}
