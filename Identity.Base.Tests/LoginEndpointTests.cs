using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Identity.Base.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using Xunit;

namespace Identity.Base.Tests;

public class LoginEndpointTests : IClassFixture<IdentityApiFactory>
{
    private readonly IdentityApiFactory _factory;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public LoginEndpointTests(IdentityApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Login_AllowsAuthorizationCodeFlow()
    {
        const string email = "login-success@example.com";
        const string password = "StrongPass!2345";

        await SeedUserAsync(email, password, confirmEmail: true);

        using var client = CreateClientWithCookies();

        var discovery = await client.GetAsync("/.well-known/openid-configuration");
        discovery.StatusCode.Should().Be(HttpStatusCode.OK, "discovery endpoint should be available");
        var discoveryDocument = await discovery.Content.ReadFromJsonAsync<JsonDocument>();
        discoveryDocument.Should().NotBeNull();
        discoveryDocument!.RootElement.GetProperty("authorization_endpoint").GetString()
            .Should().Be("https://localhost/connect/authorize");

        var loginResponse = await client.PostAsJsonAsync("/auth/login", new
        {
            email,
            password,
            clientId = "spa-client"
        });

        var loginPayload = await loginResponse.Content.ReadFromJsonAsync<JsonDocument>();
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK, loginPayload?.RootElement.ToString());

        var pkce = PkceData.Create();
        var state = Guid.NewGuid().ToString("N");
        const string redirectUri = "https://localhost:3000/auth/callback";

        var authorizeUrl = QueryHelpers.AddQueryString("/connect/authorize", new Dictionary<string, string?>
        {
            [OpenIddictConstants.Parameters.ResponseType] = OpenIddictConstants.ResponseTypes.Code,
            [OpenIddictConstants.Parameters.ClientId] = "spa-client",
            [OpenIddictConstants.Parameters.RedirectUri] = redirectUri,
            [OpenIddictConstants.Parameters.Scope] = "openid profile email offline_access identity.api",
            [OpenIddictConstants.Parameters.CodeChallenge] = pkce.CodeChallenge,
            [OpenIddictConstants.Parameters.CodeChallengeMethod] = OpenIddictConstants.CodeChallengeMethods.Sha256,
            [OpenIddictConstants.Parameters.State] = state
        });

        using var authorizeResponse = await client.GetAsync(authorizeUrl);
        var authorizePayload = await authorizeResponse.Content.ReadAsStringAsync();
        authorizeResponse.StatusCode.Should().Be(
            HttpStatusCode.Redirect,
            "Response content: {0}",
            authorizePayload);
        authorizeResponse.Headers.Location.Should().NotBeNull();

        var location = authorizeResponse.Headers.Location!;
        var callbackQuery = QueryHelpers.ParseQuery(location.Query);

        callbackQuery.Should().ContainKey(OpenIddictConstants.Parameters.Code);
        callbackQuery.Should().ContainKey(OpenIddictConstants.Parameters.State);
        callbackQuery[OpenIddictConstants.Parameters.State].ToString().Should().Be(state);

        var authorizationCode = callbackQuery[OpenIddictConstants.Parameters.Code].ToString();

        // Exchange authorization code for tokens
        using var tokenResponse = await client.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            [OpenIddictConstants.Parameters.GrantType] = OpenIddictConstants.GrantTypes.AuthorizationCode,
            [OpenIddictConstants.Parameters.Code] = authorizationCode,
            [OpenIddictConstants.Parameters.RedirectUri] = redirectUri,
            [OpenIddictConstants.Parameters.ClientId] = "spa-client",
            [OpenIddictConstants.Parameters.CodeVerifier] = pkce.CodeVerifier
        }));

        var tokenPayloadJson = await tokenResponse.Content.ReadAsStringAsync();
        tokenResponse.IsSuccessStatusCode.Should().BeTrue(tokenPayloadJson);

        var tokenPayload = JsonSerializer.Deserialize<TokenResponse>(tokenPayloadJson, JsonOptions);
        tokenPayload.Should().NotBeNull();
        tokenPayload!.AccessToken.Should().NotBeNullOrWhiteSpace();
        tokenPayload.RefreshToken.Should().NotBeNullOrWhiteSpace();
        tokenPayload.TokenType.Should().Be("Bearer");

        using var refreshResponse = await client.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            [OpenIddictConstants.Parameters.GrantType] = OpenIddictConstants.GrantTypes.RefreshToken,
            [OpenIddictConstants.Parameters.RefreshToken] = tokenPayload.RefreshToken,
            [OpenIddictConstants.Parameters.ClientId] = "spa-client"
        }));

        var refreshPayloadJson = await refreshResponse.Content.ReadAsStringAsync();
        refreshResponse.IsSuccessStatusCode.Should().BeTrue(refreshPayloadJson);

        var refreshPayload = JsonSerializer.Deserialize<TokenResponse>(refreshPayloadJson, JsonOptions);
        refreshPayload.Should().NotBeNull();
        refreshPayload!.AccessToken.Should().NotBeNullOrWhiteSpace();
        refreshPayload.RefreshToken.Should().NotBeNullOrWhiteSpace();
        refreshPayload.AccessToken.Should().NotBe(tokenPayload.AccessToken);
    }

    [Fact]
    public async Task Login_Fails_WhenEmailNotConfirmed()
    {
        const string email = "login-unconfirmed@example.com";
        const string password = "StrongPass!2345";

        await SeedUserAsync(email, password, confirmEmail: false);

        using var client = CreateClientWithCookies();

        var response = await client.PostAsJsonAsync("/auth/login", new
        {
            email,
            password,
            clientId = "spa-client"
        });

        var payloadJson = await response.Content.ReadAsStringAsync();
        response.IsSuccessStatusCode.Should().BeFalse(payloadJson);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private HttpClient CreateClientWithCookies()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        client.BaseAddress = new Uri("https://localhost");
        return client;
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
                DisplayName = "Test User"
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

    private sealed record TokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; init; } = string.Empty;

        [JsonPropertyName("refresh_token")]
        public string RefreshToken { get; init; } = string.Empty;

        [JsonPropertyName("token_type")]
        public string TokenType { get; init; } = string.Empty;

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; init; }
    }

    private sealed record PkceData(string CodeVerifier, string CodeChallenge)
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
}
