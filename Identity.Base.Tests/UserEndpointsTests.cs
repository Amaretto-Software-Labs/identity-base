using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Shouldly;
using Identity.Base.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Identity;
using Xunit;

namespace Identity.Base.Tests;

[Collection("AdminEndpoints")]
public class UserEndpointsTests : IClassFixture<IdentityApiFactory>
{
    private readonly IdentityApiFactory _factory;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public UserEndpointsTests(IdentityApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ChangePassword_UpdatesCredentials()
    {
        const string email = "change-password-success@example.com";
        const string originalPassword = "StrongPass!2345";
        const string newPassword = "NewStrongPass!2345";

        await SeedUserAsync(email, originalPassword, confirmEmail: true);

        using var client = await CreateAuthenticatedClientAsync(email, originalPassword);

        var response = await client.PostAsJsonAsync("/users/me/change-password", new
        {
            currentPassword = originalPassword,
            newPassword,
            confirmNewPassword = newPassword
        }, JsonOptions);

        var payload = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent, payload);

        // old password should no longer work
        using (var unauthenticatedClient = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = true,
            AllowAutoRedirect = false
        }))
        {
            var oldLogin = await unauthenticatedClient.PostAsJsonAsync("/auth/login", new
            {
                email,
                password = originalPassword,
                clientId = "spa-client"
            });

            var oldPayload = await oldLogin.Content.ReadAsStringAsync();
            oldLogin.StatusCode.ShouldBe(HttpStatusCode.BadRequest, oldPayload);
        }

        using (var unauthenticatedClient = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = true,
            AllowAutoRedirect = false
        }))
        {
            var newLogin = await unauthenticatedClient.PostAsJsonAsync("/auth/login", new
            {
                email,
                password = newPassword,
                clientId = "spa-client"
            });

            var newPayload = await newLogin.Content.ReadAsStringAsync();
            newLogin.StatusCode.ShouldBe(HttpStatusCode.OK, newPayload);
        }
    }

    [Fact]
    public async Task ChangePassword_ReturnsValidationError_WhenCurrentPasswordIncorrect()
    {
        const string email = "change-password-fail@example.com";
        const string originalPassword = "StrongPass!2345";

        await SeedUserAsync(email, originalPassword, confirmEmail: true);

        using var client = await CreateAuthenticatedClientAsync(email, originalPassword);

        var response = await client.PostAsJsonAsync("/users/me/change-password", new
        {
            currentPassword = "WrongPassword!234",
            newPassword = "AnotherStrongPass!2345",
            confirmNewPassword = "AnotherStrongPass!2345"
        }, JsonOptions);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ChangePassword_ReturnsValidationError_WhenNewPasswordViolatesPolicy()
    {
        const string email = "change-password-policy@example.com";
        const string originalPassword = "StrongPass!2345";

        await SeedUserAsync(email, originalPassword, confirmEmail: true);

        using var client = await CreateAuthenticatedClientAsync(email, originalPassword);

        var response = await client.PostAsJsonAsync("/users/me/change-password", new
        {
            currentPassword = originalPassword,
            newPassword = "short",
            confirmNewPassword = "short"
        }, JsonOptions);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        problem.ShouldNotBeNull();
        problem!.Errors.ShouldNotBeEmpty();
        problem.Errors.SelectMany(kvp => kvp.Value).ShouldContain(error => error.Contains("Passwords must be at least", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ChangePassword_ReturnsValidationError_WhenConfirmationDoesNotMatch()
    {
        const string email = "change-password-confirmation@example.com";
        const string originalPassword = "StrongPass!2345";
        const string candidatePassword = "AnotherStrongPass!2345";

        await SeedUserAsync(email, originalPassword, confirmEmail: true);

        using var client = await CreateAuthenticatedClientAsync(email, originalPassword);

        var response = await client.PostAsJsonAsync("/users/me/change-password", new
        {
            currentPassword = originalPassword,
            newPassword = candidatePassword,
            confirmNewPassword = candidatePassword + "mismatch"
        }, JsonOptions);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        problem.ShouldNotBeNull();
        problem!.Errors.ShouldContainKey("ConfirmNewPassword");
    }

    [Fact]
    public async Task GetCurrentUser_ReturnsProfile_WhenAuthenticatedWithBearerToken()
    {
        const string email = "users-me-bearer@example.com";
        const string password = "StrongPass!2345";

        await SeedUserAsync(email, password, confirmEmail: true);

        var accessToken = await _factory.CreateAccessTokenAsync(email, password, scope: "openid profile email identity.api");
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = false,
            AllowAutoRedirect = false
        });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.GetAsync("/users/me");
        var payload = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, payload);

        using var document = JsonDocument.Parse(payload);
        document.RootElement.GetProperty("email").GetString().ShouldBe(email);
        document.RootElement.GetProperty("concurrencyStamp").GetString().ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task UpdateProfile_UpdatesMetadata_WhenAuthenticatedWithBearerToken()
    {
        const string email = "users-me-profile-bearer@example.com";
        const string password = "StrongPass!2345";
        const string updatedDisplayName = "Updated Bearer User";
        const string updatedCompany = "Acme Bearer Co";

        await SeedUserAsync(email, password, confirmEmail: true);

        var accessToken = await _factory.CreateAccessTokenAsync(email, password, scope: "openid profile email identity.api");
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = false,
            AllowAutoRedirect = false
        });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var meResponse = await client.GetAsync("/users/me");
        var mePayload = await meResponse.Content.ReadAsStringAsync();
        meResponse.StatusCode.ShouldBe(HttpStatusCode.OK, mePayload);

        using var meDocument = JsonDocument.Parse(mePayload);
        var concurrencyStamp = meDocument.RootElement.GetProperty("concurrencyStamp").GetString();
        concurrencyStamp.ShouldNotBeNullOrWhiteSpace();

        var response = await client.PutAsJsonAsync("/users/me/profile", new
        {
            concurrencyStamp,
            metadata = new
            {
                displayName = updatedDisplayName,
                company = updatedCompany
            }
        }, JsonOptions);

        var payload = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, payload);

        using var document = JsonDocument.Parse(payload);
        document.RootElement.GetProperty("displayName").GetString().ShouldBe(updatedDisplayName);
        document.RootElement.GetProperty("metadata").GetProperty("company").GetString().ShouldBe(updatedCompany);
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
                DisplayName = "Change Password User"
            };

            var createResult = await userManager.CreateAsync(user, password);
            createResult.Succeeded.ShouldBeTrue(createResult.Errors.FirstOrDefault()?.Description);
        }
        else
        {
            await userManager.RemovePasswordAsync(user);
            await userManager.AddPasswordAsync(user, password);
            user.EmailConfirmed = confirmEmail;
            await userManager.UpdateAsync(user);
        }
    }

    private async Task<HttpClient> CreateAuthenticatedClientAsync(string email, string password)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = true,
            AllowAutoRedirect = false
        });

        var loginResponse = await client.PostAsJsonAsync("/auth/login", new
        {
            email,
            password,
            clientId = "spa-client"
        }, JsonOptions);

        var payload = await loginResponse.Content.ReadAsStringAsync();
        loginResponse.StatusCode.ShouldBe(HttpStatusCode.OK, payload);

        return client;
    }
}
