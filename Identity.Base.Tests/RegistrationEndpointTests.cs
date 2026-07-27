using System.Net;
using System.Net.Http.Json;
using Shouldly;
using Identity.Base.Data;
using Identity.Base.Features.Email;
using Identity.Base.Options;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Identity.Base.Tests;

public class RegistrationEndpointTests : IClassFixture<IdentityApiFactory>
{
    private readonly IdentityApiFactory _factory;

    public RegistrationEndpointTests(IdentityApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task RegisterUser_PersistsMetadata_And_SendsConfirmationEmail()
    {
        using var client = _factory.CreateClient();

        var uniqueEmail = $"newuser-{Guid.NewGuid():N}@example.com";

        var payload = new
        {
            email = uniqueEmail,
            password = "Passw0rd!Passw0rd!",
            metadata = new
            {
                displayName = "New User",
                company = "Acme"
            }
        };

        var response = await client.PostAsJsonAsync("/auth/register", payload);

        var responseBody = await response.Content.ReadAsStringAsync();
        if (response.StatusCode != HttpStatusCode.Accepted)
        {
            throw new Xunit.Sdk.XunitException($"Registration failed with status {(int)response.StatusCode}: {responseBody}");
        }

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var createdUser = await dbContext.Users.SingleAsync(user => user.Email == uniqueEmail);
        createdUser.DisplayName.ShouldBe("New User");
        createdUser.ProfileMetadata.Values.ShouldContainKey("company");
        createdUser.ProfileMetadata.Values["company"].ShouldBe("Acme");

        var email = _factory.EmailSender.Sent
            .Where(item => item.ToEmail == uniqueEmail)
            .ShouldHaveSingleItem();
        email.ToEmail.ShouldBe(uniqueEmail);
        email.TemplateKey.ShouldBe(TemplatedEmailKeys.AccountConfirmation);
        email.Variables.ShouldContainKey("confirmationUrl");
    }

    [Fact]
    public async Task RegisterUser_UsesConfiguredFullNameAsDisplayName()
    {
        using var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.PostConfigure<RegistrationOptions>(options =>
                {
                    options.ProfileFields = new List<RegistrationProfileFieldOptions>
                    {
                        new()
                        {
                            Name = "fullName",
                            DisplayName = "Full name",
                            Required = true,
                            MaxLength = 128
                        }
                    };
                });
            });
        });
        using var client = factory.CreateClient();
        var uniqueEmail = $"full-name-{Guid.NewGuid():N}@example.com";

        var response = await client.PostAsJsonAsync("/auth/register", new
        {
            email = uniqueEmail,
            password = "Passw0rd!Passw0rd!",
            metadata = new { fullName = "Configured Full Name" }
        });

        var responseBody = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.Accepted, responseBody);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var createdUser = await dbContext.Users.SingleAsync(user => user.Email == uniqueEmail);
        createdUser.DisplayName.ShouldBe("Configured Full Name");
    }

    [Fact]
    public async Task RegisterUser_RemovesUser_WhenConfirmationEmailFails()
    {
        using var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ITemplatedEmailSender>();
                services.AddSingleton<ITemplatedEmailSender, ThrowingEmailSender>();
            });
        });
        using var client = factory.CreateClient();
        var uniqueEmail = $"failed-registration-{Guid.NewGuid():N}@example.com";

        var response = await client.PostAsJsonAsync("/auth/register", new
        {
            email = uniqueEmail,
            password = "Passw0rd!Passw0rd!",
            metadata = new { displayName = "Failed Registration" }
        });

        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await dbContext.Users.AnyAsync(user => user.Email == uniqueEmail)).ShouldBeFalse();
    }

    private sealed class ThrowingEmailSender : ITemplatedEmailSender
    {
        public Task SendAsync(TemplatedEmail email, CancellationToken cancellationToken = default)
            => Task.FromException(new InvalidOperationException("Simulated email failure."));
    }
}
