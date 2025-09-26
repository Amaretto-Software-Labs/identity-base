using System;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Identity.Base.Identity;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
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
        startResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);
        var callbackLocation = startResponse.Headers.Location;
        callbackLocation.Should().NotBeNull();

        var callbackResponse = await client.GetAsync(callbackLocation);
        callbackResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);
        var finalLocation = callbackResponse.Headers.Location;
        finalLocation.Should().NotBeNull();

        var uri = new Uri(client.BaseAddress!, finalLocation!);
        var query = QueryHelpers.ParseQuery(uri.Query);
        query["status"].ToString().Should().Be("success");
        query["requiresTwoFactor"].ToString().Should().Be("false");

        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync("login-new@example.com");
        user.Should().NotBeNull();
        var logins = await userManager.GetLoginsAsync(user!);
        logins.Should().ContainSingle(login => login.LoginProvider == GoogleDefaults.AuthenticationScheme);
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

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
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

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        var location = response.Headers.Location;
        location.Should().NotBeNull();
        location!.Host.Should().Be("localhost");
        location.Scheme.Should().Be("https");
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

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ExternalLink_And_Unlink_Works()
    {
        const string email = "link-user@example.com";
        const string password = "StrongPass!2345";

        await SeedUserAsync(email, password);

        using var client = await CreateAuthenticatedClientAsync(email, password);
        var linkStart = await client.GetAsync("/auth/external/google/start?mode=link&returnUrl=/link/result&email=link@example.com&name=Linked");
        linkStart.StatusCode.Should().Be(HttpStatusCode.Redirect);
        var callback = linkStart.Headers.Location;
        callback.Should().NotBeNull();

        var callbackResponse = await client.GetAsync(callback);
        callbackResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);
        var finalLocation = callbackResponse.Headers.Location;
        var finalUri = new Uri(client.BaseAddress!, finalLocation!);
        var query = QueryHelpers.ParseQuery(finalUri.Query);
        query["status"].ToString().Should().Be("linked");

        using (var scope = _factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByEmailAsync(email);
            user.Should().NotBeNull();
            var logins = await userManager.GetLoginsAsync(user!);
            logins.Should().ContainSingle(login => login.LoginProvider == GoogleDefaults.AuthenticationScheme);
        }

        var unlinkResponse = await client.DeleteAsync("/auth/external/google");
        unlinkResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        using (var scope = _factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByEmailAsync(email);
            user.Should().NotBeNull();
            var logins = await userManager.GetLoginsAsync(user!);
            logins.Should().BeEmpty();
        }
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
            result.Succeeded.Should().BeTrue();
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
        loginResponse.IsSuccessStatusCode.Should().BeTrue(body);
        return client;
    }
}
