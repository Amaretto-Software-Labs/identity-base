using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Identity.Base.Features.Email;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Shouldly;

namespace Identity.Base.Tests;

public sealed class PasskeyEndpointTests : IClassFixture<IdentityApiFactory>
{
    private readonly IdentityApiFactory _factory;

    public PasskeyEndpointTests(IdentityApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Configuration_AdvertisesBothSignupModes()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync("/auth/passkeys/configuration");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var payload = await response.Content.ReadFromJsonAsync<JsonDocument>();
        payload.ShouldNotBeNull();
        payload.RootElement.GetProperty("enabled").GetBoolean().ShouldBeTrue();
        payload.RootElement.GetProperty("signupModes")
            .EnumerateArray()
            .Select(element => element.GetString())
            .ShouldBe(["passkey-assisted", "passwordless"], ignoreOrder: true);
    }

    [Fact]
    public async Task AuthenticationOptions_RejectsConfidentialClient()
    {
        using var client = _factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/auth/passkeys/authentication/options",
            new { clientId = "test-client" });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("passkey-assisted")]
    [InlineData("passwordless")]
    public async Task Signup_EmailConfirmation_IssuesPreUserCreationOptions(string mode)
    {
        _factory.EmailSender.Clear();
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = true,
            AllowAutoRedirect = false
        });
        var email = $"passkey-{mode}-{Guid.NewGuid():N}@example.com";

        using var begin = await client.PostAsJsonAsync("/auth/passkeys/registration/begin", new
        {
            mode,
            email,
            clientId = "spa-client",
            metadata = new { displayName = "Passkey User" }
        });

        var beginBody = await begin.Content.ReadAsStringAsync();
        begin.StatusCode.ShouldBe(HttpStatusCode.Accepted, beginBody);
        var sent = _factory.EmailSender.Sent
            .Where(message => message.ToEmail == email)
            .ShouldHaveSingleItem();
        sent.TemplateKey.ShouldBe(TemplatedEmailKeys.PasskeySignupConfirmation);
        var confirmationUrl = sent.Variables["confirmationUrl"]?.ToString();
        confirmationUrl.ShouldNotBeNullOrWhiteSpace();
        var confirmationUri = new Uri(confirmationUrl);
        var query = QueryHelpers.ParseQuery(confirmationUri.Query);

        using var confirmation = await client.PostAsJsonAsync(
            "/auth/passkeys/registration/confirm-email",
            new
            {
                draftId = query["draftId"].ToString(),
                token = query["token"].ToString()
            });
        var confirmationBody = await confirmation.Content.ReadAsStringAsync();
        confirmation.StatusCode.ShouldBe(HttpStatusCode.NoContent, confirmationBody);

        using var options = await client.PostAsJsonAsync(
            "/auth/passkeys/registration/creation/options",
            new { draftId = query["draftId"].ToString() });
        var optionsBody = await options.Content.ReadAsStringAsync();
        options.StatusCode.ShouldBe(HttpStatusCode.OK, optionsBody);
        options.Content.Headers.ContentType?.MediaType.ShouldBe("application/json");
        using var optionsJson = JsonDocument.Parse(optionsBody);
        optionsJson.RootElement.GetProperty("challenge").GetString().ShouldNotBeNullOrWhiteSpace();
        optionsJson.RootElement.GetProperty("user").GetProperty("id").GetString().ShouldNotBeNullOrWhiteSpace();
    }
}
