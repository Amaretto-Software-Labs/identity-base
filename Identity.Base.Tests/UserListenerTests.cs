using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
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
using Microsoft.AspNetCore.WebUtilities;
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

    [Fact]
    public async Task ResetPassword_InvokesPasswordResetLifecycleHooks()
    {
        var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.AddSingleton<TestPasswordLifecycleListener>();
                services.AddScoped<IUserLifecycleListener>(sp => sp.GetRequiredService<TestPasswordLifecycleListener>());
            });
        });

        const string email = "lifecycle-reset@example.com";
        const string password = "ResetInitial!2345";
        var userId = await SeedUserAsync(factory, email, password);

        string encodedToken;
        using (var scope = factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByIdAsync(userId.ToString());
            user.ShouldNotBeNull();
            var token = await userManager.GeneratePasswordResetTokenAsync(user!);
            encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
        }

        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/auth/reset-password", new
        {
            userId = userId.ToString(),
            token = encodedToken,
            password = "ResetNew!4567"
        });
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, body);

        var listener = factory.Services.GetRequiredService<TestPasswordLifecycleListener>();
        listener.BeforeReset.ShouldBeTrue();
        listener.AfterReset.ShouldBeTrue();
    }

    [Fact]
    public async Task ChangePassword_InvokesPasswordChangeLifecycleHooks()
    {
        var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.AddSingleton<TestPasswordLifecycleListener>();
                services.AddScoped<IUserLifecycleListener>(sp => sp.GetRequiredService<TestPasswordLifecycleListener>());
            });
        });

        const string email = "lifecycle-change@example.com";
        const string password = "ChangeInitial!2345";
        await SeedUserAsync(factory, email, password);

        using var client = await CreateAuthenticatedClientAsync(factory, email, password);
        var response = await client.PostAsJsonAsync("/users/me/change-password", new
        {
            currentPassword = password,
            newPassword = "ChangeFinal!6789",
            confirmNewPassword = "ChangeFinal!6789"
        });
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent, body);

        var listener = factory.Services.GetRequiredService<TestPasswordLifecycleListener>();
        listener.BeforeChange.ShouldBeTrue();
        listener.AfterChange.ShouldBeTrue();
    }

    [Fact]
    public async Task AdminCreateUser_InvokesLifecycleHooks()
    {
        var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.AddSingleton<AdminLifecycleListener>();
                services.AddScoped<IUserLifecycleListener>(sp => sp.GetRequiredService<AdminLifecycleListener>());
            });
        });

        var (_, token) = await CreateAdminUserAndTokenAsync(factory, "admin-listener@example.com", "AdminPass!2345", includeAdminScope: true);
        using var client = CreateAuthorizedClient(factory, token);

        var response = await client.PostAsJsonAsync("/admin/users", new
        {
            Email = $"admin-created-{Guid.NewGuid():N}@example.com"
        }, JsonOptions);
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.Created, body);

        var listener = factory.Services.GetRequiredService<AdminLifecycleListener>();
        listener.AdminRegistrations.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task AdminLockUnlockUser_InvokesLifecycleHooks()
    {
        var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.AddSingleton<AdminLifecycleListener>();
                services.AddScoped<IUserLifecycleListener>(sp => sp.GetRequiredService<AdminLifecycleListener>());
            });
        });

        var (_, token) = await CreateAdminUserAndTokenAsync(factory, "admin-lock@example.com", "AdminPass!2345", includeAdminScope: true);
        using var client = CreateAuthorizedClient(factory, token);

        var createResponse = await client.PostAsJsonAsync("/admin/users", new { Email = $"lock-target-{Guid.NewGuid():N}@example.com" }, JsonOptions);
        var body = await createResponse.Content.ReadAsStringAsync();
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created, body);
        var targetUserId = await ExtractUserIdAsync(createResponse);

        var lockResponse = await client.PostAsJsonAsync($"/admin/users/{targetUserId:D}/lock", new { minutes = 5 });
        var lockBody = await lockResponse.Content.ReadAsStringAsync();
        lockResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent, lockBody);

        var unlockResponse = await client.PostAsync($"/admin/users/{targetUserId:D}/unlock", null);
        var unlockBody = await unlockResponse.Content.ReadAsStringAsync();
        unlockResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent, unlockBody);

        var listener = factory.Services.GetRequiredService<AdminLifecycleListener>();
        listener.BeforeLock.ShouldBeTrue();
        listener.AfterLock.ShouldBeTrue();
        listener.BeforeUnlock.ShouldBeTrue();
        listener.AfterUnlock.ShouldBeTrue();
    }

    [Fact]
    public async Task AdminUpdateRoles_InvokesLifecycleHooks()
    {
        var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.AddSingleton<AdminLifecycleListener>();
                services.AddScoped<IUserLifecycleListener>(sp => sp.GetRequiredService<AdminLifecycleListener>());
            });
        });

        var (_, token) = await CreateAdminUserAndTokenAsync(factory, "admin-roles@example.com", "AdminPass!2345", includeAdminScope: true);
        using var client = CreateAuthorizedClient(factory, token);

        var createResponse = await client.PostAsJsonAsync("/admin/users", new { Email = $"roles-target-{Guid.NewGuid():N}@example.com" }, JsonOptions);
        var body = await createResponse.Content.ReadAsStringAsync();
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created, body);
        var targetUserId = await ExtractUserIdAsync(createResponse);

        var updateResponse = await client.PutAsJsonAsync($"/admin/users/{targetUserId:D}/roles", new
        {
            roles = new[] { "IdentityAdmin" }
        });
        var updateBody = await updateResponse.Content.ReadAsStringAsync();
        updateResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent, updateBody);

        var listener = factory.Services.GetRequiredService<AdminLifecycleListener>();
        listener.RolesUpdated.ShouldBeTrue();
    }

    [Fact]
    public async Task AdminResetMfa_InvokesLifecycleHooks()
    {
        var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.AddSingleton<AdminLifecycleListener>();
                services.AddScoped<IUserLifecycleListener>(sp => sp.GetRequiredService<AdminLifecycleListener>());
            });
        });

        var (_, token) = await CreateAdminUserAndTokenAsync(factory, "admin-mfa@example.com", "AdminPass!2345", includeAdminScope: true);
        using var client = CreateAuthorizedClient(factory, token);

        var createResponse = await client.PostAsJsonAsync("/admin/users", new { Email = $"mfa-target-{Guid.NewGuid():N}@example.com" }, JsonOptions);
        var body = await createResponse.Content.ReadAsStringAsync();
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created, body);
        var targetUserId = await ExtractUserIdAsync(createResponse);

        var resetResponse = await client.PostAsync($"/admin/users/{targetUserId:D}/mfa/reset", null);
        var resetBody = await resetResponse.Content.ReadAsStringAsync();
        resetResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent, resetBody);

        var listener = factory.Services.GetRequiredService<AdminLifecycleListener>();
        listener.BeforeMfaReset.ShouldBeTrue();
        listener.AfterMfaReset.ShouldBeTrue();
    }

    [Fact]
    public async Task ResendConfirmation_InvokesEmailConfirmationRequestedHook()
    {
        var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.AddSingleton<NotificationLifecycleListener>();
                services.AddScoped<IUserLifecycleListener>(sp => sp.GetRequiredService<NotificationLifecycleListener>());
            });
        });

        const string email = "resend-hook@example.com";
        await SeedUserAsync(factory, email, "StrongPass!123");
        using (var scope = factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByEmailAsync(email);
            user!.EmailConfirmed = false;
            await userManager.UpdateAsync(user);
        }

        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/auth/resend-confirmation", new { email });
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.Accepted, body);

        var listener = factory.Services.GetRequiredService<NotificationLifecycleListener>();
        listener.EmailConfirmationRequested.ShouldBeTrue();
    }

    [Fact]
    public async Task ForgotPassword_InvokesPasswordResetRequestedHook()
    {
        var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.AddSingleton<NotificationLifecycleListener>();
                services.AddScoped<IUserLifecycleListener>(sp => sp.GetRequiredService<NotificationLifecycleListener>());
            });
        });

        const string email = "forgot-hook@example.com";
        await SeedUserAsync(factory, email, "StrongPass!123");

        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/auth/forgot-password", new { email });
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.Accepted, body);

        var listener = factory.Services.GetRequiredService<NotificationLifecycleListener>();
        listener.PasswordResetRequested.ShouldBeTrue();
    }

    [Fact]
    public async Task DisableMfa_InvokesLifecycleHooks()
    {
        var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.AddSingleton<NotificationLifecycleListener>();
                services.AddScoped<IUserLifecycleListener>(sp => sp.GetRequiredService<NotificationLifecycleListener>());
            });
        });

        const string email = "disable-mfa@example.com";
        const string password = "StrongPass!123";
        await SeedUserAsync(factory, email, password);

        using var client = await CreateAuthenticatedClientAsync(factory, email, password);

        using (var scope = factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByEmailAsync(email);
            user!.TwoFactorEnabled = true;
            await userManager.UpdateAsync(user);
        }

        var response = await client.PostAsync("/auth/mfa/disable", null);
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, body);

        var listener = factory.Services.GetRequiredService<NotificationLifecycleListener>();
        listener.MfaDisabled.ShouldBeTrue();
    }

    [Fact]
    public async Task RegenerateRecoveryCodes_InvokesLifecycleHooks()
    {
        var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.AddSingleton<NotificationLifecycleListener>();
                services.AddScoped<IUserLifecycleListener>(sp => sp.GetRequiredService<NotificationLifecycleListener>());
            });
        });

        const string email = "recovery-mfa@example.com";
        const string password = "StrongPass!123";
        await SeedUserAsync(factory, email, password);

        using var client = await CreateAuthenticatedClientAsync(factory, email, password);
        using (var scope = factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByEmailAsync(email);
            user!.TwoFactorEnabled = true;
            await userManager.UpdateAsync(user);
        }

        var response = await client.PostAsync("/auth/mfa/recovery-codes", null);
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, body);

        var listener = factory.Services.GetRequiredService<NotificationLifecycleListener>();
        listener.RecoveryCodesGenerated.ShouldBeTrue();
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

    private async Task<(Guid UserId, string AccessToken)> CreateAdminUserAndTokenAsync(
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

        var accessToken = await _factory.CreateAccessTokenAsync(email, password, factory: factory, scope: scopeValue);
        accessToken.ShouldNotBeNullOrWhiteSpace();

        return (user.Id, accessToken);
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

    private sealed class TestPasswordLifecycleListener : IUserLifecycleListener
    {
        public bool BeforeReset { get; private set; }
        public bool AfterReset { get; private set; }
        public bool BeforeChange { get; private set; }
        public bool AfterChange { get; private set; }

        public ValueTask<LifecycleHookResult> BeforeUserPasswordResetAsync(UserLifecycleContext context, CancellationToken cancellationToken = default)
        {
            BeforeReset = true;
            return ValueTask.FromResult(LifecycleHookResult.Continue());
        }

        public ValueTask AfterUserPasswordResetAsync(UserLifecycleContext context, CancellationToken cancellationToken = default)
        {
            AfterReset = true;
            return ValueTask.CompletedTask;
        }

        public ValueTask<LifecycleHookResult> BeforeUserPasswordChangedAsync(UserLifecycleContext context, CancellationToken cancellationToken = default)
        {
            BeforeChange = true;
            return ValueTask.FromResult(LifecycleHookResult.Continue());
        }

        public ValueTask AfterUserPasswordChangedAsync(UserLifecycleContext context, CancellationToken cancellationToken = default)
        {
            AfterChange = true;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class AdminLifecycleListener : IUserLifecycleListener
    {
        public int AdminRegistrations { get; private set; }
        public bool BeforeLock { get; private set; }
        public bool AfterLock { get; private set; }
        public bool BeforeUnlock { get; private set; }
        public bool AfterUnlock { get; private set; }
        public bool RolesUpdated { get; private set; }
        public bool BeforeMfaReset { get; private set; }
        public bool AfterMfaReset { get; private set; }
        public bool MfaDisabled { get; private set; }

        public ValueTask AfterUserRegisteredAsync(UserLifecycleContext context, CancellationToken cancellationToken = default)
        {
            AdminRegistrations++;
            return ValueTask.CompletedTask;
        }

        public ValueTask<LifecycleHookResult> BeforeUserLockedAsync(UserLifecycleContext context, CancellationToken cancellationToken = default)
        {
            BeforeLock = true;
            return ValueTask.FromResult(LifecycleHookResult.Continue());
        }

        public ValueTask AfterUserLockedAsync(UserLifecycleContext context, CancellationToken cancellationToken = default)
        {
            AfterLock = true;
            return ValueTask.CompletedTask;
        }

        public ValueTask<LifecycleHookResult> BeforeUserUnlockedAsync(UserLifecycleContext context, CancellationToken cancellationToken = default)
        {
            BeforeUnlock = true;
            return ValueTask.FromResult(LifecycleHookResult.Continue());
        }

        public ValueTask AfterUserUnlockedAsync(UserLifecycleContext context, CancellationToken cancellationToken = default)
        {
            AfterUnlock = true;
            return ValueTask.CompletedTask;
        }

        public ValueTask<LifecycleHookResult> BeforeUserRolesUpdatedAsync(UserLifecycleContext context, CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(LifecycleHookResult.Continue());
        }

        public ValueTask AfterUserRolesUpdatedAsync(UserLifecycleContext context, CancellationToken cancellationToken = default)
        {
            RolesUpdated = true;
            return ValueTask.CompletedTask;
        }

        public ValueTask<LifecycleHookResult> BeforeUserMfaResetAsync(UserLifecycleContext context, CancellationToken cancellationToken = default)
        {
            BeforeMfaReset = true;
            return ValueTask.FromResult(LifecycleHookResult.Continue());
        }

        public ValueTask AfterUserMfaResetAsync(UserLifecycleContext context, CancellationToken cancellationToken = default)
        {
            AfterMfaReset = true;
            return ValueTask.CompletedTask;
        }

        public ValueTask AfterUserMfaDisabledAsync(UserLifecycleContext context, CancellationToken cancellationToken = default)
        {
            MfaDisabled = true;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class NotificationLifecycleListener : IUserLifecycleListener
    {
        public bool EmailConfirmationRequested { get; private set; }
        public bool PasswordResetRequested { get; private set; }
        public bool MfaDisabled { get; private set; }
        public bool RecoveryCodesGenerated { get; private set; }

        public ValueTask<LifecycleHookResult> BeforeEmailConfirmationRequestedAsync(UserLifecycleContext context, CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(LifecycleHookResult.Continue());
        }

        public ValueTask AfterEmailConfirmationRequestedAsync(UserLifecycleContext context, CancellationToken cancellationToken = default)
        {
            EmailConfirmationRequested = true;
            return ValueTask.CompletedTask;
        }

        public ValueTask<LifecycleHookResult> BeforePasswordResetRequestedAsync(UserLifecycleContext context, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(LifecycleHookResult.Continue());

        public ValueTask AfterPasswordResetRequestedAsync(UserLifecycleContext context, CancellationToken cancellationToken = default)
        {
            PasswordResetRequested = true;
            return ValueTask.CompletedTask;
        }

        public ValueTask<LifecycleHookResult> BeforeUserMfaDisabledAsync(UserLifecycleContext context, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(LifecycleHookResult.Continue());

        public ValueTask AfterUserMfaDisabledAsync(UserLifecycleContext context, CancellationToken cancellationToken = default)
        {
            MfaDisabled = true;
            return ValueTask.CompletedTask;
        }

        public ValueTask<LifecycleHookResult> BeforeRecoveryCodesGeneratedAsync(UserLifecycleContext context, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(LifecycleHookResult.Continue());

        public ValueTask AfterRecoveryCodesGeneratedAsync(UserLifecycleContext context, CancellationToken cancellationToken = default)
        {
            RecoveryCodesGenerated = true;
            return ValueTask.CompletedTask;
        }
    }
}
