using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Shouldly;
using Identity.Base.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Identity.Base.Tests;

public class ExternalAuthenticationTests : IClassFixture<IdentityApiFactory>
{
    private readonly IdentityApiFactory _factory;

    public ExternalAuthenticationTests(IdentityApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ExternalLogin_CreatesUser_WhenNew()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });
        client.BaseAddress = new Uri("https://localhost");

        var startResponse = await client.GetAsync("/auth/external/google/start?returnUrl=/client/callback&email=login-new@example.com&name=Login%20User");
        startResponse.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        var callbackLocation = startResponse.Headers.Location;
        callbackLocation.ShouldNotBeNull();

        var callbackResponse = await client.GetAsync(callbackLocation);
        callbackResponse.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        var finalLocation = callbackResponse.Headers.Location;
        finalLocation.ShouldNotBeNull();

        var uri = new Uri(client.BaseAddress!, finalLocation!);
        var query = QueryHelpers.ParseQuery(uri.Query);
        query["status"].ToString().ShouldBe("success");
        query["requiresTwoFactor"].ToString().ShouldBe("false");

        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync("login-new@example.com");
        user.ShouldNotBeNull();
        var logins = await userManager.GetLoginsAsync(user!);
        logins.ShouldContain(login => login.LoginProvider == IdentityApiFactory.FakeGoogleScheme);
        logins.Count(login => login.LoginProvider == IdentityApiFactory.FakeGoogleScheme).ShouldBe(1);
    }

    [Fact]
    public async Task ExternalLogin_CreatesUser_ForCustomRegisteredProvider()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });
        client.BaseAddress = new Uri("https://localhost");

        var startResponse = await client.GetAsync("/auth/external/github/start?returnUrl=/client/callback&email=github-new@example.com&name=Github%20User");
        startResponse.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        var callbackLocation = startResponse.Headers.Location;
        callbackLocation.ShouldNotBeNull();

        var callbackResponse = await client.GetAsync(callbackLocation);
        callbackResponse.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        var finalLocation = callbackResponse.Headers.Location;
        finalLocation.ShouldNotBeNull();

        var uri = new Uri(client.BaseAddress!, finalLocation!);
        var query = QueryHelpers.ParseQuery(uri.Query);
        query["status"].ToString().ShouldBe("success");
        query["requiresTwoFactor"].ToString().ShouldBe("false");

        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync("github-new@example.com");
        user.ShouldNotBeNull();
        var logins = await userManager.GetLoginsAsync(user!);
        logins.ShouldContain(login => string.Equals(login.LoginProvider, "GitHub", StringComparison.OrdinalIgnoreCase));
        logins.Count(login => string.Equals(login.LoginProvider, "GitHub", StringComparison.OrdinalIgnoreCase)).ShouldBe(1);
    }

    [Fact]
    public async Task ExternalLogin_DoesNotAutoLinkByEmail_WhenDisabled()
    {
        const string email = "strict-linking@example.com";
        const string password = "StrongPass!2345";

        using var strictFactory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configurationBuilder) =>
            {
                var overrides = new Dictionary<string, string?>
                {
                    ["Authentication:External:AutoLinkByEmailOnLogin"] = "false"
                };
                configurationBuilder.AddInMemoryCollection(overrides);
            });
        });

        using (var scope = strictFactory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var existing = await userManager.FindByEmailAsync(email);
            if (existing is null)
            {
                var created = await userManager.CreateAsync(new ApplicationUser
                {
                    Email = email,
                    UserName = email,
                    EmailConfirmed = true,
                    DisplayName = "Strict Linking User"
                }, password);
                created.Succeeded.ShouldBeTrue();
            }
        }

        using var client = strictFactory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });
        client.BaseAddress = new Uri("https://localhost");

        var startResponse = await client.GetAsync($"/auth/external/google/start?returnUrl=/client/callback&email={Uri.EscapeDataString(email)}&name=Strict%20Linking");
        startResponse.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        var callbackLocation = startResponse.Headers.Location;
        callbackLocation.ShouldNotBeNull();

        var callbackResponse = await client.GetAsync(callbackLocation);
        callbackResponse.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        var finalLocation = callbackResponse.Headers.Location;
        finalLocation.ShouldNotBeNull();

        var uri = new Uri(client.BaseAddress!, finalLocation!);
        var query = QueryHelpers.ParseQuery(uri.Query);
        query["status"].ToString().ShouldBe("error");
        query["message"].ToString().ShouldContain("not linked");

        using var verifyScope = strictFactory.Services.CreateScope();
        var verifyUserManager = verifyScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await verifyUserManager.FindByEmailAsync(email);
        user.ShouldNotBeNull();
        var logins = await verifyUserManager.GetLoginsAsync(user!);
        logins.ShouldNotContain(login => login.LoginProvider == IdentityApiFactory.FakeGoogleScheme);
    }

    [Fact]
    public async Task ExternalLogin_RequiresVerifiedEmail_ForAutoLink_WhenEnabled()
    {
        const string email = "verified-required@example.com";
        const string password = "StrongPass!2345";

        using var strictFactory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configurationBuilder) =>
            {
                var overrides = new Dictionary<string, string?>
                {
                    ["Authentication:External:AutoLinkByEmailOnLogin"] = "true",
                    ["Authentication:External:RequireVerifiedEmailForAutoLinkByEmail"] = "true"
                };
                configurationBuilder.AddInMemoryCollection(overrides);
            });
        });

        using (var scope = strictFactory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var existing = await userManager.FindByEmailAsync(email);
            if (existing is null)
            {
                var created = await userManager.CreateAsync(new ApplicationUser
                {
                    Email = email,
                    UserName = email,
                    EmailConfirmed = true,
                    DisplayName = "Verified Required User"
                }, password);
                created.Succeeded.ShouldBeTrue();
            }
        }

        using var client = strictFactory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });
        client.BaseAddress = new Uri("https://localhost");

        var startResponse = await client.GetAsync($"/auth/external/google/start?returnUrl=/client/callback&email={Uri.EscapeDataString(email)}&name=Verified%20Required&emailVerified=false");
        startResponse.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        var callbackLocation = startResponse.Headers.Location;
        callbackLocation.ShouldNotBeNull();

        var callbackResponse = await client.GetAsync(callbackLocation);
        callbackResponse.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        var finalLocation = callbackResponse.Headers.Location;
        finalLocation.ShouldNotBeNull();

        var uri = new Uri(client.BaseAddress!, finalLocation!);
        var query = QueryHelpers.ParseQuery(uri.Query);
        query["status"].ToString().ShouldBe("error");
        query["message"].ToString().ShouldContain("not verified");

        using var verifyScope = strictFactory.Services.CreateScope();
        var verifyUserManager = verifyScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await verifyUserManager.FindByEmailAsync(email);
        user.ShouldNotBeNull();
        var logins = await verifyUserManager.GetLoginsAsync(user!);
        logins.ShouldNotContain(login => login.LoginProvider == IdentityApiFactory.FakeGoogleScheme);
    }

    [Fact]
    public async Task ExternalLogin_StartAllowsConfiguredAbsoluteReturnUrl()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });
        client.BaseAddress = new Uri("https://localhost");

        // https://localhost:3000 is configured in appsettings RedirectUris
        var encoded = Uri.EscapeDataString("https://localhost:3000/auth/external-complete");
        var response = await client.GetAsync($"/auth/external/google/start?returnUrl={encoded}&email=absolute@example.com&name=Absolute");

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
    }

    [Fact]
    public async Task ExternalLogin_StartIgnoresForwardedHeaders()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });
        client.BaseAddress = new Uri("https://localhost");

        var request = new HttpRequestMessage(HttpMethod.Get, "/auth/external/google/start?returnUrl=/client/callback&email=fh@example.com&name=Forwarded");
        request.Headers.TryAddWithoutValidation("X-Forwarded-Host", "evil.com");
        request.Headers.TryAddWithoutValidation("X-Forwarded-Proto", "http");

        var response = await client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        var location = response.Headers.Location;
        location.ShouldNotBeNull();
        location!.Host.ShouldBe("localhost");
        location.Scheme.ShouldBe("https");
    }

    [Fact]
    public async Task ExternalLogin_StartRejectsUnregisteredProvider()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });
        client.BaseAddress = new Uri("https://localhost");

        var response = await client.GetAsync("/auth/external/Identity.External/start?returnUrl=/client/callback");
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("//evil.com")]
    [InlineData("https://evil.com/callback")]
    [InlineData("http://evil.com/callback")]
    [InlineData("client/callback")]
    public async Task ExternalLogin_StartRejectsUnsafeReturnUrls(string unsafeReturnUrl)
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });
        client.BaseAddress = new Uri("https://localhost");

        var encoded = Uri.EscapeDataString(unsafeReturnUrl);
        var response = await client.GetAsync($"/auth/external/google/start?returnUrl={encoded}&email=malicious@example.com&name=bad");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ExternalLink_And_Unlink_Works()
    {
        const string email = "link-user@example.com";
        const string password = "StrongPass!2345";

        await SeedUserAsync(email, password);

        using var client = await CreateAuthenticatedClientAsync(email, password);
        var linkStart = await client.GetAsync("/auth/external/google/start?mode=link&returnUrl=/link/result&email=link@example.com&name=Linked");
        linkStart.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        var callback = linkStart.Headers.Location;
        callback.ShouldNotBeNull();

        var callbackResponse = await client.GetAsync(callback);
        callbackResponse.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        var finalLocation = callbackResponse.Headers.Location;
        var finalUri = new Uri(client.BaseAddress!, finalLocation!);
        var query = QueryHelpers.ParseQuery(finalUri.Query);
        query["status"].ToString().ShouldBe("linked");

        using (var scope = _factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByEmailAsync(email);
            user.ShouldNotBeNull();
            var logins = await userManager.GetLoginsAsync(user!);
            logins.ShouldContain(login => login.LoginProvider == IdentityApiFactory.FakeGoogleScheme);
            logins.Count(login => login.LoginProvider == IdentityApiFactory.FakeGoogleScheme).ShouldBe(1);
        }

        var unlinkResponse = await client.DeleteAsync("/auth/external/google");
        unlinkResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        using (var scope = _factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByEmailAsync(email);
            user.ShouldNotBeNull();
            var logins = await userManager.GetLoginsAsync(user!);
            logins.ShouldBeEmpty();
        }
    }

    [Fact]
    public async Task ExternalUnlink_AllowsBearerAuthentication()
    {
        const string email = "unlink-bearer@example.com";
        const string password = "StrongPass!2345";

        await SeedUserAsync(email, password);

        using (var scope = _factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByEmailAsync(email);
            user.ShouldNotBeNull();

            var addLoginResult = await userManager.AddLoginAsync(
                user!,
                new UserLoginInfo(IdentityApiFactory.FakeGoogleScheme, "unlink-bearer-key", "Google"));
            addLoginResult.Succeeded.ShouldBeTrue();
        }

        var accessToken = await _factory.CreateAccessTokenAsync(email, password, _factory, "openid profile email offline_access identity.api");

        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = false
        });
        client.BaseAddress = new Uri("https://localhost");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var unlinkResponse = await client.DeleteAsync("/auth/external/google");
        unlinkResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        using (var scope = _factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByEmailAsync(email);
            user.ShouldNotBeNull();
            var logins = await userManager.GetLoginsAsync(user!);
            logins.ShouldBeEmpty();
        }
    }

    [Fact]
    public async Task ExternalUnlink_RejectsLastSignInMethod_ForExternalOnlyAccount()
    {
        const string email = "external-only@example.com";

        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });
        client.BaseAddress = new Uri("https://localhost");

        var startResponse = await client.GetAsync($"/auth/external/google/start?returnUrl=/client/callback&email={Uri.EscapeDataString(email)}&name=External%20Only");
        startResponse.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        var callbackLocation = startResponse.Headers.Location;
        callbackLocation.ShouldNotBeNull();

        var callbackResponse = await client.GetAsync(callbackLocation);
        callbackResponse.StatusCode.ShouldBe(HttpStatusCode.Redirect);

        var unlinkResponse = await client.DeleteAsync("/auth/external/google");
        unlinkResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var body = await unlinkResponse.Content.ReadAsStringAsync();
        body.ShouldContain("Cannot unlink the last sign-in method");

        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(email);
        user.ShouldNotBeNull();
        var logins = await userManager.GetLoginsAsync(user!);
        logins.Count(login => login.LoginProvider == IdentityApiFactory.FakeGoogleScheme).ShouldBe(1);
        (await userManager.HasPasswordAsync(user!)).ShouldBeFalse();
    }

    private async Task SeedUserAsync(string email, string password)
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            user = new ApplicationUser
            {
                Email = email,
                UserName = email,
                EmailConfirmed = true,
                DisplayName = "External Test User"
            };

            var result = await userManager.CreateAsync(user, password);
            result.Succeeded.ShouldBeTrue();
        }
        else if (!user.EmailConfirmed)
        {
            user.EmailConfirmed = true;
            await userManager.UpdateAsync(user);
        }
    }

    private async Task<HttpClient> CreateAuthenticatedClientAsync(string email, string password)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });
        client.BaseAddress = new Uri("https://localhost");

        var loginResponse = await client.PostAsJsonAsync("/auth/login", new
        {
            email,
            password,
            clientId = "spa-client"
        });

        var body = await loginResponse.Content.ReadAsStringAsync();
        loginResponse.IsSuccessStatusCode.ShouldBeTrue(body);
        return client;
    }
}
