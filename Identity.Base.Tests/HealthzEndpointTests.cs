using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Identity.Base.Data;
using Identity.Base.Features.Authentication.Mfa;
using Identity.Base.Features.Email;
using Identity.Base.Identity;
using Identity.Base.Options;
using Identity.Base.Tests.Fakes;
using Identity.Base.Roles.Options;
using OpenIddict.Abstractions;
using OpenIddict.Server;
using OpenIddict.Server.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
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

        var checks = payload.RootElement.GetProperty("checks").EnumerateArray().Select(element => element.GetProperty("name").GetString()).ToList();
        checks.Should().Contain("database");
        checks.Should().Contain("mailjet");
        checks.Should().Contain("externalProviders");
    }
}

public class IdentityApiFactory : WebApplicationFactory<Program>
{
    public FakeEmailSender EmailSender { get; } = new();
    public FakeMfaChallengeSender SmsChallengeSender { get; } = new();

    private static readonly Uri DefaultBaseAddress = new("https://localhost");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting(WebHostDefaults.EnvironmentKey, Environments.Development);

        builder.ConfigureServices(services =>
        {
            services.RemoveAll(typeof(ITemplatedEmailSender));
            services.AddSingleton<FakeEmailSender>(_ => EmailSender);
            services.AddSingleton<ITemplatedEmailSender>(provider => provider.GetRequiredService<FakeEmailSender>());
            services.RemoveAll(typeof(IMfaChallengeSender));
            services.AddScoped<IMfaChallengeSender, EmailMfaChallengeSender>();
            services.AddSingleton<FakeMfaChallengeSender>(_ => SmsChallengeSender);
            services.AddSingleton<IMfaChallengeSender>(provider => provider.GetRequiredService<FakeMfaChallengeSender>());

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
                options.PasswordResetUrlTemplate = "https://tests.example.com/reset?token={token}&email={email}";
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
                options.Templates.PasswordReset = 5678;
                options.Templates.MfaChallenge = 6789;
                options.ErrorReporting.Enabled = false;
            });

            services.PostConfigure<MfaOptions>(options =>
            {
                options.Email.Enabled = true;
                options.Sms.Enabled = true;
                options.Sms.AccountSid = "test";
                options.Sms.AuthToken = "test";
                options.Sms.FromPhoneNumber = "+15005550006";
            });

            services.PostConfigure<ExternalProviderOptions>(options =>
            {
                options.Google.Enabled = true;
                options.Google.ClientId = "test";
                options.Google.ClientSecret = "secret";
                options.Google.CallbackPath = "/signin-google";
                options.Google.Scopes.Clear();
                options.Google.Scopes.Add("openid");
                options.Google.Scopes.Add("profile");
                options.Google.Scopes.Add("email");
            });

            services.AddAuthentication()
                .AddScheme<AuthenticationSchemeOptions, FakeExternalAuthenticationHandler>(GoogleDefaults.AuthenticationScheme, _ => { });

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
                    ClientType = OpenIddictConstants.ClientTypes.Confidential,
                    ClientSecret = "test-secret",
                    RedirectUris = { "https://localhost/callback" },
                    Permissions =
                    {
                        OpenIddictConstants.Permissions.Endpoints.Token,
                        OpenIddictConstants.Permissions.Endpoints.Authorization,
                        OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode,
                        OpenIddictConstants.Permissions.GrantTypes.Password,
                        OpenIddictConstants.Permissions.ResponseTypes.Code,
                        OpenIddictConstants.Permissions.Prefixes.Scope + OpenIddictConstants.Scopes.Email,
                        OpenIddictConstants.Permissions.Prefixes.Scope + OpenIddictConstants.Scopes.Profile,
                        OpenIddictConstants.Permissions.Prefixes.Scope + "identity.api",
                        OpenIddictConstants.Permissions.Prefixes.Scope + "identity.admin"
                    },
                    AllowPasswordFlow = true
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
                        OpenIddictConstants.Permissions.Prefixes.Scope + "identity.api",
                        OpenIddictConstants.Permissions.Prefixes.Scope + "identity.admin"
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
                options.Scopes.Add(new OpenIddictScopeOptions
                {
                    Name = "identity.admin",
                    DisplayName = "Identity Admin API",
                    Resources = { "identity.api", "identity.admin" }
                });
            });

            services.PostConfigure<OpenIddictServerAspNetCoreOptions>(options =>
            {
                options.DisableTransportSecurityRequirement = true;
            });

            services.PostConfigure<PermissionCatalogOptions>(options =>
            {
                options.Definitions.Clear();
                options.Definitions.Add(new PermissionDefinition { Name = "users.read", Description = "View user directory" });
                options.Definitions.Add(new PermissionDefinition { Name = "users.manage-roles", Description = "Assign roles" });
                options.Definitions.Add(new PermissionDefinition { Name = "users.update", Description = "Update user profiles" });
                options.Definitions.Add(new PermissionDefinition { Name = "roles.read", Description = "View role definitions" });
            });

            services.PostConfigure<RoleConfigurationOptions>(options =>
            {
                options.Definitions.Clear();
                options.Definitions.Add(new RoleDefinition
                {
                    Name = "StandardUser",
                    Description = "Standard user",
                    Permissions = new List<string>()
                });

                options.Definitions.Add(new RoleDefinition
                {
                    Name = "IdentityAdmin",
                    Description = "Full admin",
                    Permissions = new List<string>
                    {
                        "users.read",
                        "users.manage-roles",
                        "users.update",
                        "roles.read"
                    },
                    IsSystemRole = true
                });

                options.DefaultUserRoles.Clear();
                options.DefaultUserRoles.Add("StandardUser");
                options.DefaultAdminRoles.Clear();
                options.DefaultAdminRoles.Add("IdentityAdmin");
            });
        });
    }

    protected override void ConfigureClient(HttpClient client)
    {
        base.ConfigureClient(client);

        if (client.BaseAddress is null || client.BaseAddress.Scheme != Uri.UriSchemeHttps)
        {
            client.BaseAddress = DefaultBaseAddress;
        }
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

    public void Clear() => _sent.Clear();
}

public sealed class FakeMfaChallengeSender : IMfaChallengeSender
{
    private readonly List<(string PhoneNumber, string Code)> _challenges = new();

    public IReadOnlyList<(string PhoneNumber, string Code)> Challenges => _challenges;

    public string Method => "sms";

    public Task SendChallengeAsync(ApplicationUser user, string code, CancellationToken cancellationToken = default)
    {
        _challenges.Add((user.PhoneNumber ?? string.Empty, code));
        return Task.CompletedTask;
    }

    public void Clear() => _challenges.Clear();
}
