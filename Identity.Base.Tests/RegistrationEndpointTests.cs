using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Identity.Base.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

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
        createdUser.DisplayName.Should().Be("New User");
        createdUser.ProfileMetadata.Values.Should().ContainKey("company");
        createdUser.ProfileMetadata.Values["company"].Should().Be("Acme");

        var email = _factory.EmailSender.Sent.Should().ContainSingle().Subject;
        email.ToEmail.Should().Be(uniqueEmail);
        email.TemplateId.Should().Be(1234);
        email.Variables.Should().ContainKey("confirmationUrl");
    }
}
