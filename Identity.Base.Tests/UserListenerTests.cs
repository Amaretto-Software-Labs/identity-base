using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Identity.Base.Abstractions;
using Identity.Base.Identity;
using Identity.Base.Roles.Services;
using Identity.Base.Roles.Configuration;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using Shouldly;
using Xunit;
using Identity.Base.Lifecycle;

namespace Identity.Base.Tests;

public class UserListenerTests : IClassFixture<IdentityApiFactory>
{
    private readonly IdentityApiFactory _factory;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public UserListenerTests(IdentityApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task UpdateProfile_InvokesUserUpdateListeners()
    {
        var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.AddSingleton<TestUserUpdateListener>();
                services.AddScoped<IUserUpdateListener>(sp => sp.GetRequiredService<TestUserUpdateListener>());
            });
        });

        const string email = "listener-profile@example.com";
        const string password = "StrongPass!2345";
        var userId = await SeedUserAsync(factory, email, password);

        using var client = await CreateAuthenticatedClientAsync(factory, email, password);
        var profile = await client.GetFromJsonAsync<UserProfilePayload>("/users/me");
        profile.ShouldNotBeNull();

        var response = await client.PutAsJsonAsync("/users/me/profile", new
        {
            metadata = new Dictionary<string, string?>
            {
                ["displayName"] = "Listener Updated",
                ["company"] = "Listener Co"
            },
            concurrencyStamp = profile!.ConcurrencyStamp
        });
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, body);

        var listener = factory.Services.GetRequiredService<TestUserUpdateListener>();
        listener.Updated.ShouldContain(userId);
    }

    [Fact]
    public async Task SoftDeleteUser_InvokesDeletionListener()
    {
        var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.AddSingleton<TestUserDeletionListener>();
                services.AddScoped<IUserDeletionListener>(sp => sp.GetRequiredService<TestUserDeletionListener>());
            });
        });

        var (_, token) = await CreateAdminUserAndTokenAsync(factory, "listener-admin-delete@example.com", "AdminPass!2345", includeAdminScope: true);
        using var client = CreateAuthorizedClient(factory, token);

        var createResponse = await client.PostAsJsonAsync("/admin/users", new { Email = "listener-delete@example.com" }, JsonOptions);
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var targetUserId = await ExtractUserIdAsync(createResponse);

        var deleteResponse = await client.DeleteAsync($"/admin/users/{targetUserId:D}");
        deleteResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var listener = factory.Services.GetRequiredService<TestUserDeletionListener>();
        listener.Deleted.ShouldContain(targetUserId);
    }

    [Fact]
    public async Task RestoreUser_InvokesRestoreListener()
    {
        var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.AddSingleton<TestUserRestoreListener>();
                services.AddScoped<IUserRestoreListener>(sp => sp.GetRequiredService<TestUserRestoreListener>());
            });
        });

        var (_, token) = await CreateAdminUserAndTokenAsync(factory, "listener-admin-restore@example.com", "AdminPass!2345", includeAdminScope: true);
        using var client = CreateAuthorizedClient(factory, token);

        var createResponse = await client.PostAsJsonAsync("/admin/users", new { Email = "listener-restore@example.com" }, JsonOptions);
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var targetUserId = await ExtractUserIdAsync(createResponse);

        var deleteResponse = await client.DeleteAsync($"/admin/users/{targetUserId:D}");
        deleteResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var restoreResponse = await client.PostAsync($"/admin/users/{targetUserId:D}/restore", null);
        restoreResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var listener = factory.Services.GetRequiredService<TestUserRestoreListener>();
        listener.Restored.ShouldContain(targetUserId);
    }

    [Fact]
    public async Task RegisterUser_BeforeLifecycleHookBlocksRegistration()
    {
        var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.AddSingleton<RejectingLifecycleListener>();
                services.AddScoped<IUserLifecycleListener>(sp => sp.GetRequiredService<RejectingLifecycleListener>());
            });
        });

        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/auth/register", new
        {
            email = $"blocked-{Guid.NewGuid():N}@example.com",
            password = "StrongPass!2345",
            metadata = new Dictionary<string, string?>
            {
                ["displayName"] = "Blocked User"
            }
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        using var scope = factory.Services.CreateScope();
        var listener = scope.ServiceProvider.GetRequiredService<RejectingLifecycleListener>();
        listener.BeforeRegistrationCalls.ShouldBeGreaterThan(0);
    }

    private static async Task<Guid> SeedUserAsync(WebApplicationFactory<Program> factory, string email, string password)
    {
        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            user = new ApplicationUser
            {
                Email = email,
                UserName = email,
                EmailConfirmed = true,
                DisplayName = "Listener User"
            };

            var createResult = await userManager.CreateAsync(user, password);
            createResult.Succeeded.ShouldBeTrue(createResult.Errors.FirstOrDefault()?.Description);
        }

        user.EmailConfirmed = true;
        await userManager.UpdateAsync(user);
        return user.Id;
    }

    private static async Task<HttpClient> CreateAuthenticatedClientAsync(WebApplicationFactory<Program> factory, string email, string password)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true,
            BaseAddress = new Uri("https://localhost")
        });

        var request = new
        {
            email,
            password,
            clientId = "spa-client"
        };

        var response = await client.PostAsJsonAsync("/auth/login", request);
        var body = await response.Content.ReadAsStringAsync();
        response.IsSuccessStatusCode.ShouldBeTrue(body);
        return client;
    }

    private static async Task<(Guid UserId, string AccessToken)> CreateAdminUserAndTokenAsync(
        WebApplicationFactory<Program> factory,
        string email,
        string password,
        bool includeAdminScope)
    {
        using var scope = factory.Services.CreateScope();
        await scope.ServiceProvider.SeedIdentityRolesAsync();
        var roleAssignmentService = scope.ServiceProvider.GetRequiredService<IRoleAssignmentService>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            user = new ApplicationUser
            {
                Email = email,
                UserName = email,
                EmailConfirmed = true,
                DisplayName = "Listener Admin"
            };
            var createResult = await userManager.CreateAsync(user, password);
            createResult.Succeeded.ShouldBeTrue(createResult.Errors.FirstOrDefault()?.Description);
        }

        await roleAssignmentService.AssignRolesAsync(user.Id, new[] { "IdentityAdmin" });

        var scopeValue = includeAdminScope
            ? string.Join(' ', new[] { OpenIddictConstants.Scopes.OpenId, OpenIddictConstants.Scopes.Profile, OpenIddictConstants.Scopes.Email, "identity.api", "identity.admin" })
            : string.Join(' ', new[] { OpenIddictConstants.Scopes.OpenId, OpenIddictConstants.Scopes.Profile, OpenIddictConstants.Scopes.Email, "identity.api" });

        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = false,
            BaseAddress = new Uri("https://localhost")
        });

        using var tokenRequest = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            [OpenIddictConstants.Parameters.GrantType] = OpenIddictConstants.GrantTypes.Password,
            [OpenIddictConstants.Parameters.Username] = email,
            [OpenIddictConstants.Parameters.Password] = password,
            [OpenIddictConstants.Parameters.ClientId] = "test-client",
            [OpenIddictConstants.Parameters.ClientSecret] = "test-secret",
            [OpenIddictConstants.Parameters.Scope] = scopeValue
        });

        var tokenResponse = await client.PostAsync("/connect/token", tokenRequest);
        var tokenPayload = await tokenResponse.Content.ReadFromJsonAsync<JsonDocument>();
        tokenResponse.StatusCode.ShouldBe(HttpStatusCode.OK, tokenPayload?.RootElement.ToString());

        var accessToken = tokenPayload!.RootElement.GetProperty("access_token").GetString();
        accessToken.ShouldNotBeNullOrWhiteSpace();

        return (user.Id, accessToken!);
    }

    private static HttpClient CreateAuthorizedClient(WebApplicationFactory<Program> factory, string accessToken)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = false,
            BaseAddress = new Uri("https://localhost")
        });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }

    private static async Task<Guid> ExtractUserIdAsync(HttpResponseMessage response)
    {
        var location = response.Headers.Location;
        if (location is not null)
        {
            var segments = location.ToString().TrimEnd('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length > 0 && Guid.TryParse(segments[^1], out var idFromLocation))
            {
                return idFromLocation;
            }
        }

        var body = await response.Content.ReadFromJsonAsync<JsonDocument>();
        body.ShouldNotBeNull();
        var id = body!.RootElement.TryGetProperty("id", out var idProperty)
            ? idProperty.GetGuid()
            : body.RootElement.GetProperty("user").GetProperty("id").GetGuid();
        return id;
    }

    private sealed record UserProfilePayload(
        Guid Id,
        string? Email,
        bool EmailConfirmed,
        string? DisplayName,
        Dictionary<string, string?> Metadata,
        string ConcurrencyStamp);

    private sealed class TestUserUpdateListener : IUserUpdateListener
    {
        public List<Guid> Updated { get; } = new();

        public Task OnUserUpdatedAsync(ApplicationUser user, CancellationToken cancellationToken = default)
        {
            Updated.Add(user.Id);
            return Task.CompletedTask;
        }
    }

    private sealed class TestUserDeletionListener : IUserDeletionListener
    {
        public List<Guid> Deleted { get; } = new();

        public Task OnUserDeletedAsync(ApplicationUser user, CancellationToken cancellationToken = default)
        {
            Deleted.Add(user.Id);
            return Task.CompletedTask;
        }
    }

    private sealed class TestUserRestoreListener : IUserRestoreListener
    {
        public List<Guid> Restored { get; } = new();

        public Task OnUserRestoredAsync(ApplicationUser user, CancellationToken cancellationToken = default)
        {
            Restored.Add(user.Id);
            return Task.CompletedTask;
        }
    }

    private sealed class RejectingLifecycleListener : IUserLifecycleListener
    {
        public int BeforeRegistrationCalls { get; private set; }

        public ValueTask<LifecycleHookResult> BeforeUserRegisteredAsync(UserLifecycleContext context, CancellationToken cancellationToken = default)
        {
            BeforeRegistrationCalls++;
            return ValueTask.FromResult(LifecycleHookResult.Fail("Registration disabled."));
        }
    }
}
