using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Identity.Base.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Identity;
using OpenIddict.Abstractions;
using Xunit;

namespace Identity.Base.Tests;

public class AuthorizeEndpointTests : IClassFixture<IdentityApiFactory>
{
    private readonly IdentityApiFactory _factory;

    public AuthorizeEndpointTests(IdentityApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Authorize_WhenPromptIsNoneAndUserNotAuthenticated_ReturnsLoginRequiredError()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        const string redirectUri = "https://localhost:3000/auth/callback";
        var state = Guid.NewGuid().ToString("N");
        var pkce = PkceData.Create();
        var query = new Dictionary<string, string?>
        {
            [OpenIddictConstants.Parameters.ResponseType] = OpenIddictConstants.ResponseTypes.Code,
            [OpenIddictConstants.Parameters.ClientId] = "spa-client",
            [OpenIddictConstants.Parameters.RedirectUri] = redirectUri,
            [OpenIddictConstants.Parameters.Scope] = "openid profile email",
            [OpenIddictConstants.Parameters.Prompt] = OpenIddictConstants.Prompts.None,
            [OpenIddictConstants.Parameters.State] = state,
            [OpenIddictConstants.Parameters.CodeChallenge] = pkce.CodeChallenge,
            [OpenIddictConstants.Parameters.CodeChallengeMethod] = OpenIddictConstants.CodeChallengeMethods.Sha256
        };

        var authorizeUrl = QueryHelpers.AddQueryString("/connect/authorize", query);

        using var response = await client.GetAsync(authorizeUrl);
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location.Should().NotBeNull();

        var location = response.Headers.Location!;
        location.AbsoluteUri.Should().StartWith(redirectUri);

        var callbackQuery = QueryHelpers.ParseQuery(location.Query);
        callbackQuery[OpenIddictConstants.Parameters.Error].ToString().Should().Be(OpenIddictConstants.Errors.LoginRequired);
        callbackQuery[OpenIddictConstants.Parameters.State].ToString().Should().Be(state);
        callbackQuery.Should().ContainKey(OpenIddictConstants.Parameters.ErrorDescription);
    }

    [Fact]
    public async Task Authorize_WhenUserNotAuthenticated_Returns401()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        const string redirectUri = "https://localhost:3000/auth/callback";
        var pkce = PkceData.Create();

        var authorizeUrl = QueryHelpers.AddQueryString("/connect/authorize", new Dictionary<string, string?>
        {
            [OpenIddictConstants.Parameters.ResponseType] = OpenIddictConstants.ResponseTypes.Code,
            [OpenIddictConstants.Parameters.ClientId] = "spa-client",
            [OpenIddictConstants.Parameters.RedirectUri] = redirectUri,
            [OpenIddictConstants.Parameters.Scope] = "openid profile",
            [OpenIddictConstants.Parameters.CodeChallenge] = pkce.CodeChallenge,
            [OpenIddictConstants.Parameters.CodeChallengeMethod] = OpenIddictConstants.CodeChallengeMethods.Sha256,
        });

        using var response = await client.GetAsync(authorizeUrl);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Headers.WwwAuthenticate.ToString().Should().Contain("login_required");
    }

    [Fact]
    public async Task Authorize_WhenPromptConsentAndUserAuthenticated_ReturnsAuthorizationCode()
    {
        const string email = "authorize-consent@example.com";
        const string password = "StrongPass!2345";
        await SeedUserAsync(email, password, confirmEmail: true);

        using var client = await CreateAuthenticatedClientAsync(email, password);

        var pkce = PkceData.Create();
        var state = Guid.NewGuid().ToString("N");
        const string redirectUri = "https://localhost:3000/auth/callback";

        var authorizeUrl = QueryHelpers.AddQueryString("/connect/authorize", new Dictionary<string, string?>
        {
            [OpenIddictConstants.Parameters.ResponseType] = OpenIddictConstants.ResponseTypes.Code,
            [OpenIddictConstants.Parameters.ClientId] = "spa-client",
            [OpenIddictConstants.Parameters.RedirectUri] = redirectUri,
            [OpenIddictConstants.Parameters.Scope] = "openid profile",
            [OpenIddictConstants.Parameters.CodeChallenge] = pkce.CodeChallenge,
            [OpenIddictConstants.Parameters.CodeChallengeMethod] = OpenIddictConstants.CodeChallengeMethods.Sha256,
            [OpenIddictConstants.Parameters.State] = state,
            [OpenIddictConstants.Parameters.Prompt] = OpenIddictConstants.Prompts.Consent
        });

        using var response = await client.GetAsync(authorizeUrl);
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location.Should().NotBeNull();

        var location = response.Headers.Location!;
        var callbackQuery = QueryHelpers.ParseQuery(location.Query);

        callbackQuery.Should().ContainKey(OpenIddictConstants.Parameters.Code);
        callbackQuery[OpenIddictConstants.Parameters.State].ToString().Should().Be(state);
    }

    [Fact]
    public async Task Logout_ClearsSessionAndForcesReauthentication()
    {
        const string email = "authorize-logout@example.com";
        const string password = "StrongPass!2345";
        await SeedUserAsync(email, password, confirmEmail: true);

        using var client = await CreateAuthenticatedClientAsync(email, password);

        const string redirectUri = "https://localhost:3000/auth/callback";
        var pkce = PkceData.Create();
        var state = Guid.NewGuid().ToString("N");

        var authorizeUrl = QueryHelpers.AddQueryString("/connect/authorize", new Dictionary<string, string?>
        {
            [OpenIddictConstants.Parameters.ResponseType] = OpenIddictConstants.ResponseTypes.Code,
            [OpenIddictConstants.Parameters.ClientId] = "spa-client",
            [OpenIddictConstants.Parameters.RedirectUri] = redirectUri,
            [OpenIddictConstants.Parameters.Scope] = "openid profile",
            [OpenIddictConstants.Parameters.CodeChallenge] = pkce.CodeChallenge,
            [OpenIddictConstants.Parameters.CodeChallengeMethod] = OpenIddictConstants.CodeChallengeMethods.Sha256,
            [OpenIddictConstants.Parameters.State] = state
        });

        using (var authorizeResponse = await client.GetAsync(authorizeUrl))
        {
            authorizeResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);
        }

        using (var logoutResponse = await client.PostAsync("/auth/logout", new StringContent(string.Empty)))
        {
            logoutResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        var secondAuthorizeUrl = QueryHelpers.AddQueryString("/connect/authorize", new Dictionary<string, string?>
        {
            [OpenIddictConstants.Parameters.ResponseType] = OpenIddictConstants.ResponseTypes.Code,
            [OpenIddictConstants.Parameters.ClientId] = "spa-client",
            [OpenIddictConstants.Parameters.RedirectUri] = redirectUri,
            [OpenIddictConstants.Parameters.Scope] = "openid profile",
            [OpenIddictConstants.Parameters.CodeChallenge] = PkceData.Create().CodeChallenge,
            [OpenIddictConstants.Parameters.CodeChallengeMethod] = OpenIddictConstants.CodeChallengeMethods.Sha256,
            [OpenIddictConstants.Parameters.State] = Guid.NewGuid().ToString("N")
        });

        using var unauthorizedResponse = await client.GetAsync(secondAuthorizeUrl);
        unauthorizedResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private async Task SeedUserAsync(string email, string password, bool confirmEmail)
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
                EmailConfirmed = confirmEmail,
                DisplayName = "Authorize Test User"
            };

            var createResult = await userManager.CreateAsync(user, password);
            createResult.Succeeded.Should().BeTrue();
        }
        else if (confirmEmail && !user.EmailConfirmed)
        {
            user.EmailConfirmed = true;
            await userManager.UpdateAsync(user);
        }

        if (confirmEmail && !await userManager.IsEmailConfirmedAsync(user))
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

        var loginPayload = await loginResponse.Content.ReadAsStringAsync();
        loginResponse.IsSuccessStatusCode.Should().BeTrue(loginPayload);

        return client;
    }
}

internal sealed record PkceData(string CodeVerifier, string CodeChallenge)
{
    public static PkceData Create()
    {
        var verifierBytes = RandomNumberGenerator.GetBytes(32);
        var codeVerifier = Base64UrlEncode(verifierBytes);

        using var sha256 = SHA256.Create();
        var challengeBytes = sha256.ComputeHash(Encoding.ASCII.GetBytes(codeVerifier));
        var codeChallenge = Base64UrlEncode(challengeBytes);

        return new PkceData(codeVerifier, codeChallenge);
    }

    private static string Base64UrlEncode(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}
