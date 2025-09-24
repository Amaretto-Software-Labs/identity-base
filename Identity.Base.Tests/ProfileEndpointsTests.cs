using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Identity.Base.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Identity.Base.Tests;

public class ProfileEndpointsTests : IClassFixture<IdentityApiFactory>
{
    private readonly IdentityApiFactory _factory;

    public ProfileEndpointsTests(IdentityApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ProfileSchema_ReturnsConfiguredFields()
    {
        using var client = _factory.CreateClient();
        client.BaseAddress = new Uri("https://localhost");

        var response = await client.GetAsync("/auth/profile-schema");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var document = await response.Content.ReadFromJsonAsync<JsonDocument>();
        document.Should().NotBeNull();

        var fields = document!.RootElement.GetProperty("fields").EnumerateArray().ToList();
        fields.Should().HaveCountGreaterOrEqualTo(1);

        var displayNameField = fields.FirstOrDefault(element => element.GetProperty("name").GetString() == "displayName");
        displayNameField.ValueKind.Should().NotBe(JsonValueKind.Undefined);
        displayNameField.GetProperty("displayName").GetString().Should().Be("Display Name");
        displayNameField.GetProperty("required").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task GetProfile_ReturnsCurrentUserMetadata()
    {
        const string email = "profile-get@example.com";
        const string password = "StrongPass!2345";

        await SeedUserAsync(email, password, new Dictionary<string, string?>
        {
            ["displayName"] = "Profile Test",
            ["company"] = "Example Co"
        });

        using var client = await CreateAuthenticatedClientAsync(email, password);

        var response = await client.GetAsync("/users/me");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<UserProfilePayload>();
        payload.Should().NotBeNull();
        payload!.Email.Should().Be(email);
        payload.DisplayName.Should().Be("Profile Test");
        payload.Metadata.Should().ContainKey("displayName").WhoseValue.Should().Be("Profile Test");
    }

    [Fact]
    public async Task UpdateProfile_Succeeds_WithValidMetadata()
    {
        const string email = "profile-update@example.com";
        const string password = "StrongPass!2345";

        await SeedUserAsync(email, password, new Dictionary<string, string?>
        {
            ["displayName"] = "Original Name",
            ["company"] = "Original Co"
        });

        using var client = await CreateAuthenticatedClientAsync(email, password);

        var current = await client.GetFromJsonAsync<UserProfilePayload>("/users/me");
        current.Should().NotBeNull();

        var updateResponse = await client.PutAsJsonAsync("/users/me/profile", new
        {
            metadata = new Dictionary<string, string?>
            {
                ["displayName"] = "Updated Name",
                ["company"] = "Updated Co"
            },
            concurrencyStamp = current!.ConcurrencyStamp
        });

        var updateBody = await updateResponse.Content.ReadAsStringAsync();
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK, updateBody);

        var updated = await updateResponse.Content.ReadFromJsonAsync<UserProfilePayload>();
        updated.Should().NotBeNull();
        updated!.DisplayName.Should().Be("Updated Name");
        updated.Metadata["company"].Should().Be("Updated Co");
        updated.ConcurrencyStamp.Should().NotBe(current.ConcurrencyStamp);

        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(email);
        user.Should().NotBeNull();
        user!.ProfileMetadata.Values["displayName"].Should().Be("Updated Name");
    }

    [Fact]
    public async Task UpdateProfile_ReturnsConflict_WhenConcurrencyStampMismatch()
    {
        const string email = "profile-conflict@example.com";
        const string password = "StrongPass!2345";

        await SeedUserAsync(email, password, new Dictionary<string, string?>
        {
            ["displayName"] = "Conflict User",
            ["company"] = "Conflict Co"
        });

        using var client = await CreateAuthenticatedClientAsync(email, password);

        var updateResponse = await client.PutAsJsonAsync("/users/me/profile", new
        {
            metadata = new Dictionary<string, string?>
            {
                ["displayName"] = "Changed",
                ["company"] = "Changed Co"
            },
            concurrencyStamp = Guid.NewGuid().ToString("N")
        });

        updateResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task UpdateProfile_ReturnsValidationError_ForMissingRequiredField()
    {
        const string email = "profile-validation@example.com";
        const string password = "StrongPass!2345";

        await SeedUserAsync(email, password, new Dictionary<string, string?>
        {
            ["displayName"] = "Required User",
            ["company"] = "Required Co"
        });

        using var client = await CreateAuthenticatedClientAsync(email, password);
        var current = await client.GetFromJsonAsync<UserProfilePayload>("/users/me");

        var response = await client.PutAsJsonAsync("/users/me/profile", new
        {
            metadata = new Dictionary<string, string?>
            {
                ["company"] = "Updated"
            },
            concurrencyStamp = current!.ConcurrencyStamp
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<JsonDocument>();
        problem.Should().NotBeNull();
        problem!.RootElement.GetProperty("errors").TryGetProperty("metadata.displayName", out _).Should().BeTrue();
    }

    private async Task SeedUserAsync(string email, string password, IDictionary<string, string?> metadata)
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
                DisplayName = metadata.TryGetValue("displayName", out var name) && !string.IsNullOrWhiteSpace(name)
                    ? name
                    : "Profile Test User"
            };

            var result = await userManager.CreateAsync(user, password);
            result.Succeeded.Should().BeTrue();
            user = await userManager.FindByEmailAsync(email);
            user.Should().NotBeNull();
        }

        user = user ?? throw new InvalidOperationException("Failed to seed user.");
        user.EmailConfirmed = true;
        user.SetProfileMetadata(metadata);
        if (metadata.TryGetValue("displayName", out var displayName) && !string.IsNullOrWhiteSpace(displayName))
        {
            user.DisplayName = displayName;
        }

        var updateResult = await userManager.UpdateAsync(user);
        updateResult.Succeeded.Should().BeTrue();
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

    private sealed record UserProfilePayload(
        Guid Id,
        string? Email,
        bool EmailConfirmed,
        string? DisplayName,
        Dictionary<string, string?> Metadata,
        string ConcurrencyStamp);
}
