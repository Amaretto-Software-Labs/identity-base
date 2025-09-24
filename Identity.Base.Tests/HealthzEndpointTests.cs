using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Identity.Base.Data;
using Identity.Base.Features.Email;
using Identity.Base.Options;
using OpenIddict.Abstractions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;

namespace Identity.Base.Tests;

public class HealthzEndpointTests : IClassFixture<IdentityApiFactory>
{
    private readonly IdentityApiFactory _factory;

    public HealthzEndpointTests(IdentityApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetHealthz_ReturnsHealthyStatus()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/healthz");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var payload = await response.Content.ReadFromJsonAsync<JsonDocument>();
        payload.Should().NotBeNull();
        payload!.RootElement.GetProperty("status").GetString().Should().Be("Healthy");
    }
}

public class IdentityApiFactory : WebApplicationFactory<Program>
{
    public FakeEmailSender EmailSender { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting(WebHostDefaults.EnvironmentKey, Environments.Development);

        builder.ConfigureServices(services =>
        {
            services.RemoveAll(typeof(ITemplatedEmailSender));
            services.AddSingleton<FakeEmailSender>(_ => EmailSender);
            services.AddSingleton<ITemplatedEmailSender>(provider => provider.GetRequiredService<FakeEmailSender>());

            services.PostConfigure<DatabaseOptions>(options =>
            {
                options.Primary = "InMemory:IdentityBaseTests";
            });

            services.PostConfigure<HealthCheckServiceOptions>(options =>
            {
                var existingRegistration = options.Registrations.FirstOrDefault(registration => registration.Name == "database");
                if (existingRegistration is not null)
                {
                    options.Registrations.Remove(existingRegistration);
                }

                options.Registrations.Add(new HealthCheckRegistration(
                    "database",
                    _ => new PassThroughHealthCheck(),
                    null,
                    null));
            });

            services.PostConfigure<RegistrationOptions>(options =>
            {
                options.ConfirmationUrlTemplate = "https://tests.example.com/confirm?token={token}&email={email}";
                options.ProfileFields = new List<RegistrationProfileFieldOptions>
                {
                    new() { Name = "displayName", DisplayName = "Display Name", Required = true, MaxLength = 128, Pattern = null! },
                    new() { Name = "company", DisplayName = "Company", Required = false, MaxLength = 128, Pattern = null! }
                };
            });

            services.PostConfigure<MailJetOptions>(options =>
            {
                options.ApiKey = "test";
                options.ApiSecret = "secret";
                options.FromEmail = "noreply@example.com";
                options.FromName = "Identity Base";
                options.Templates.Confirmation = 1234;
                options.ErrorReporting.Enabled = false;
            });

            services.PostConfigure<CorsSettings>(options =>
            {
                options.AllowedOrigins.Clear();
                options.AllowedOrigins.Add("https://tests.example.com");
            });

            services.PostConfigure<OpenIddictOptions>(options =>
            {
                options.Applications.Clear();
                options.Applications.Add(new OpenIddictApplicationOptions
                {
                    ClientId = "test-client",
                    ClientType = OpenIddictConstants.ClientTypes.Public,
                    RedirectUris = { "https://localhost/callback" },
                    Permissions =
                    {
                        OpenIddictConstants.Permissions.Endpoints.Token,
                        OpenIddictConstants.Permissions.Endpoints.Authorization,
                        OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode,
                        OpenIddictConstants.Permissions.ResponseTypes.Code,
                        OpenIddictConstants.Permissions.Prefixes.Scope + OpenIddictConstants.Scopes.Email,
                        OpenIddictConstants.Permissions.Prefixes.Scope + OpenIddictConstants.Scopes.Profile
                    }
                });

                options.Applications.Add(new OpenIddictApplicationOptions
                {
                    ClientId = "spa-client",
                    ClientType = OpenIddictConstants.ClientTypes.Public,
                    RedirectUris = { "https://localhost:3000/auth/callback" },
                    PostLogoutRedirectUris = { "https://localhost:3000" },
                    Permissions =
                    {
                        OpenIddictConstants.Permissions.Endpoints.Authorization,
                        OpenIddictConstants.Permissions.Endpoints.Token,
                        OpenIddictConstants.Permissions.Prefixes.Endpoint + "userinfo",
                        OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode,
                        OpenIddictConstants.Permissions.ResponseTypes.Code,
                        OpenIddictConstants.Permissions.Prefixes.Scope + OpenIddictConstants.Scopes.OpenId,
                        OpenIddictConstants.Permissions.Prefixes.Scope + OpenIddictConstants.Scopes.Profile,
                        OpenIddictConstants.Permissions.Prefixes.Scope + OpenIddictConstants.Scopes.Email,
                        OpenIddictConstants.Permissions.Prefixes.Scope + OpenIddictConstants.Scopes.OfflineAccess,
                        OpenIddictConstants.Permissions.Prefixes.Scope + "identity.api"
                    },
                    Requirements =
                    {
                        OpenIddictConstants.Requirements.Features.ProofKeyForCodeExchange
                    }
                });

                options.Scopes.Clear();
                options.Scopes.Add(new OpenIddictScopeOptions
                {
                    Name = "identity.api",
                    DisplayName = "Identity API",
                    Resources = { "identity.api" }
                });
            });
        });
    }

    private sealed class PassThroughHealthCheck : IHealthCheck
    {
        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
            => Task.FromResult(HealthCheckResult.Healthy());
    }
}

public sealed class FakeEmailSender : ITemplatedEmailSender
{
    private readonly List<TemplatedEmail> _sent = new();

    public IReadOnlyCollection<TemplatedEmail> Sent => _sent.AsReadOnly();

    public Task SendAsync(TemplatedEmail email, CancellationToken cancellationToken = default)
    {
        _sent.Add(email);
        return Task.CompletedTask;
    }
}
