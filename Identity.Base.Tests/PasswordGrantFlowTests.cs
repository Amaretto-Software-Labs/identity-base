using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Shouldly;
using Identity.Base.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http.Json;
using OpenIddict.Abstractions;
using Xunit;

namespace Identity.Base.Tests;

public class PasswordGrantFlowTests : IClassFixture<IdentityApiFactory>
{
    private readonly IdentityApiFactory _factory;

    public PasswordGrantFlowTests(IdentityApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task PasswordGrant_Succeeds_ForAllowedClient()
    {
        const string email = "password-flow@example.com";
        const string password = "StrongPass!2345";

        await SeedUserAsync(email, password);

        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });
        client.BaseAddress ??= new Uri("https://localhost");

        using var request = new HttpRequestMessage(HttpMethod.Post, "/connect/token")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "password",
                ["username"] = email,
                ["password"] = password,
                ["scope"] = "openid profile email"
            })
        };
        request.Headers.Authorization = CreateBasicAuth("test-client", "test-secret");

        using var response = await client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var json = await response.Content.ReadFromJsonAsync<JsonDocument>();
        json.ShouldNotBeNull();
        json!.RootElement.GetProperty("access_token").GetString().ShouldNotBeNull();
    }

    [Fact]
    public async Task PasswordGrant_Fails_ForDisallowedClient()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });
        client.BaseAddress ??= new Uri("https://localhost");

        using var request = new HttpRequestMessage(HttpMethod.Post, "/connect/token")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "password",
                ["username"] = "someone@example.com",
                ["password"] = "does-not-matter"
            })
        };
        request.Headers.Authorization = CreateBasicAuth("spa-client", string.Empty);

        using var response = await client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var json = await response.Content.ReadFromJsonAsync<JsonDocument>();
        json.ShouldNotBeNull();
        json!.RootElement.GetProperty("error").GetString().ShouldBe("unauthorized_client");
    }

    [Fact]
    public async Task ClientCredentialsGrant_Succeeds_ForAllowedClient()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });
        client.BaseAddress ??= new Uri("https://localhost");

        using var request = new HttpRequestMessage(HttpMethod.Post, "/connect/token")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = OpenIddictConstants.GrantTypes.ClientCredentials,
                ["scope"] = "identity.api"
            })
        };
        request.Headers.Authorization = CreateBasicAuth("test-client", "test-secret");

        using var response = await client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var json = await response.Content.ReadFromJsonAsync<JsonDocument>();
        json.ShouldNotBeNull();
        json!.RootElement.GetProperty("access_token").GetString().ShouldNotBeNull();
    }

    [Fact]
    public async Task ClientCredentialsGrant_Fails_ForDisallowedClient()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });
        client.BaseAddress ??= new Uri("https://localhost");

        using var request = new HttpRequestMessage(HttpMethod.Post, "/connect/token")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = OpenIddictConstants.GrantTypes.ClientCredentials,
                ["scope"] = "identity.api"
            })
        };
        request.Headers.Authorization = CreateBasicAuth("spa-client", string.Empty);

        using var response = await client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var json = await response.Content.ReadFromJsonAsync<JsonDocument>();
        json.ShouldNotBeNull();
        json!.RootElement.GetProperty("error").GetString().ShouldBe("unauthorized_client");
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
                DisplayName = "Password Flow User"
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

    private static AuthenticationHeaderValue CreateBasicAuth(string clientId, string clientSecret)
    {
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));
        return new AuthenticationHeaderValue("Basic", credentials);
    }
}
