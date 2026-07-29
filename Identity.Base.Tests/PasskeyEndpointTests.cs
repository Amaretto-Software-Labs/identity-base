using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Identity.Base.Features.Authentication.Passkeys;
using Identity.Base.Features.Email;
using Identity.Base.Identity;
using Identity.Base.Options;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
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
    public void EmailRateLimiter_PartitionsByHashedEmailAndFlow()
    {
        using var limiter = new PasskeyEmailRateLimiter(
            Microsoft.Extensions.Options.Options.Create(new PasskeyOptions()));

        limiter.TryAcquire("signup", "ALICE@EXAMPLE.COM").ShouldBeTrue();
        limiter.TryAcquire("signup", "ALICE@EXAMPLE.COM").ShouldBeTrue();
        limiter.TryAcquire("signup", "ALICE@EXAMPLE.COM").ShouldBeTrue();
        limiter.TryAcquire("signup", "ALICE@EXAMPLE.COM").ShouldBeFalse();

        limiter.TryAcquire("signup", "BOB@EXAMPLE.COM").ShouldBeTrue();
        limiter.TryAcquire("recovery", "ALICE@EXAMPLE.COM").ShouldBeTrue();
    }

    [Fact]
    public void EmailRateLimiter_CanBeExplicitlyDisabled()
    {
        var options = new PasskeyOptions();
        options.RateLimits.Enabled = false;
        using var limiter = new PasskeyEmailRateLimiter(
            Microsoft.Extensions.Options.Options.Create(options));

        for (var attempt = 0; attempt < 10; attempt++)
        {
            limiter.TryAcquire("signup", "ALICE@EXAMPLE.COM").ShouldBeTrue();
        }
    }

    [Fact]
    public void DraftRateLimiter_PartitionsByDraftAndFlow()
    {
        var options = new PasskeyOptions();
        options.RateLimits.SignupEnrollment = new PasskeyRateLimitRule(2, 60);
        options.RateLimits.RecoveryEnrollment = new PasskeyRateLimitRule(1, 60);
        using var limiter = new PasskeyDraftRateLimiter(
            Microsoft.Extensions.Options.Options.Create(options));
        var firstDraft = Guid.NewGuid();
        var secondDraft = Guid.NewGuid();

        limiter.TryAcquire("signup", firstDraft).ShouldBeTrue();
        limiter.TryAcquire("signup", firstDraft).ShouldBeTrue();
        limiter.TryAcquire("signup", firstDraft).ShouldBeFalse();
        limiter.TryAcquire("signup", secondDraft).ShouldBeTrue();
        limiter.TryAcquire("recovery", firstDraft).ShouldBeTrue();
        limiter.TryAcquire("recovery", firstDraft).ShouldBeFalse();
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
    public async Task ProgrammaticOptions_DriveIdentitySchemaAndRateLimitPolicies()
    {
        using var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Passkeys:Enabled"] = "false"
                }));
            builder.ConfigureServices(services =>
                services.PostConfigure<PasskeyOptions>(options =>
                {
                    options.Enabled = true;
                    options.RateLimits.Configuration = new PasskeyRateLimitRule(1, 60);
                }));
        });
        using var client = factory.CreateClient();

        factory.Services.GetRequiredService<IOptions<IdentityOptions>>()
            .Value.Stores.SchemaVersion.ShouldBe(IdentitySchemaVersions.Version3);

        using var first = await client.GetAsync("/auth/passkeys/configuration");
        using var second = await client.GetAsync("/auth/passkeys/configuration");

        first.StatusCode.ShouldBe(HttpStatusCode.OK);
        second.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
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

    [Fact]
    public async Task SignupRoutes_AreNotMapped_WhenNoSignupModeIsEnabled()
    {
        using var factory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
                services.PostConfigure<PasskeyOptions>(options =>
                    options.Signup.EnabledModes.Clear())));
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/auth/passkeys/registration/begin",
            new
            {
                mode = "passwordless",
                email = "not-mapped@example.com",
                clientId = "spa-client"
            });

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PasskeyAndAdminResetRoutes_AreNotMapped_WhenPasskeysAreDisabled()
    {
        using var factory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
                services.PostConfigure<PasskeyOptions>(options =>
                {
                    options.Enabled = false;
                    options.Signup.EnabledModes.Clear();
                })));
        using var client = factory.CreateClient();

        using var configuration = await client.GetAsync("/auth/passkeys/configuration");
        using var adminReset = await client.PostAsJsonAsync(
            $"/admin/users/{Guid.NewGuid():D}/passkeys/revoke-all",
            new { reason = "test" });

        configuration.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        adminReset.StatusCode.ShouldBe(HttpStatusCode.NotFound);
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
        confirmation.StatusCode.ShouldBe(HttpStatusCode.OK, confirmationBody);
        using var confirmationJson = JsonDocument.Parse(confirmationBody);
        confirmationJson.RootElement.GetProperty("registrationMode").GetString().ShouldBe(mode);

        using var replay = await client.PostAsJsonAsync(
            "/auth/passkeys/registration/confirm-email",
            new
            {
                draftId = query["draftId"].ToString(),
                token = query["token"].ToString()
            });
        replay.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

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

    [Fact]
    public async Task Recovery_EmailConfirmation_RejectsReplay()
    {
        _factory.EmailSender.Clear();
        var email = $"passkey-recovery-{Guid.NewGuid():N}@example.com";
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                Email = email,
                UserName = email,
                EmailConfirmed = true
            };
            (await userManager.CreateAsync(user)).Succeeded.ShouldBeTrue();
        }

        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = true,
            AllowAutoRedirect = false
        });
        using var begin = await client.PostAsJsonAsync("/auth/passkeys/recovery/begin", new
        {
            email,
            clientId = "spa-client"
        });
        begin.StatusCode.ShouldBe(HttpStatusCode.Accepted);

        var sent = _factory.EmailSender.Sent
            .Where(message => message.ToEmail == email)
            .ShouldHaveSingleItem();
        var confirmationUrl = sent.Variables["confirmationUrl"]?.ToString();
        confirmationUrl.ShouldNotBeNullOrWhiteSpace();
        var query = QueryHelpers.ParseQuery(new Uri(confirmationUrl).Query);
        var request = new
        {
            draftId = query["draftId"].ToString(),
            token = query["token"].ToString()
        };

        using var confirmation = await client.PostAsJsonAsync(
            "/auth/passkeys/recovery/confirm-email",
            request);
        confirmation.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        using var replay = await client.PostAsJsonAsync(
            "/auth/passkeys/recovery/confirm-email",
            request);
        replay.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }
}
