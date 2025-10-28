using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Shouldly;
using Identity.Base.Identity;
using Identity.Base.Data;
using Identity.Base.Roles.Abstractions;
using Identity.Base.Roles.Configuration;
using Identity.Base.Roles.Services;
using Identity.Base.Tests;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Identity.Base.Tests.Admin;

[Collection("AdminEndpoints")]
public class AdminUserEndpointsTests : IClassFixture<IdentityApiFactory>
{
    private readonly IdentityApiFactory _factory;

    public AdminUserEndpointsTests(IdentityApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ListUsers_ReturnsAdminUser_WhenCallerHasPermission()
    {
        const string email = "admin-list@example.com";
        const string password = "AdminPass!2345";

        var (_, token) = await CreateAdminUserAndTokenAsync(email, password, includeAdminScope: true);

        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = false
        });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/admin/users?page=1&pageSize=10");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<AdminUserListDto>(JsonOptions);
        payload.ShouldNotBeNull();
        payload!.TotalCount.ShouldBeGreaterThan(0);
        payload.Users.ShouldContain(item => string.Equals(item.Email, email, StringComparison.OrdinalIgnoreCase));
        var adminEntry = payload.Users.First(item => string.Equals(item.Email, email, StringComparison.OrdinalIgnoreCase));
        adminEntry.Roles.ShouldContain("IdentityAdmin");
    }

    [Fact]
    public async Task GetUser_ReturnsDetail_WhenCallerHasPermission()
    {
        const string email = "admin-detail@example.com";
        const string password = "AdminPass!2345";

        var (userId, token) = await CreateAdminUserAndTokenAsync(email, password, includeAdminScope: true);

        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = false
        });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync($"/admin/users/{userId:D}");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<AdminUserDetailDto>(JsonOptions);
        payload.ShouldNotBeNull();
        payload!.Email.ShouldBe(email);
        payload.Roles.ShouldContain("IdentityAdmin");
        payload.Metadata.ShouldNotBeNull();
    }

    [Fact]
    public async Task ListUsers_ReturnsForbidden_WhenScopeMissing()
    {
        const string email = "admin-noscope@example.com";
        const string password = "AdminPass!2345";

        var (_, token) = await CreateAdminUserAndTokenAsync(email, password, includeAdminScope: false);

        using var client = CreateAuthorizedClient(token);

        var response = await client.GetAsync("/admin/users");
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ListUsers_CapsPageSize_AndAppliesSorting()
    {
        var (_, token) = await CreateAdminUserAndTokenAsync("admin-paging@example.com", "AdminPass!2345", includeAdminScope: true);

        List<string> expectedEmails;
        int expectedTotal;
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await SeedUsersAsync(dbContext, prefix: "paging-user", count: 110);

            expectedTotal = await dbContext.Users.CountAsync();
            expectedEmails = await dbContext.Users
                .AsNoTracking()
                .OrderBy(user => user.Email ?? user.UserName ?? string.Empty)
                .ThenBy(user => user.Id)
                .Select(user => user.Email ?? user.UserName ?? string.Empty)
                .Skip(100)
                .Take(100)
                .ToListAsync();
        }

        using var client = CreateAuthorizedClient(token);
        var response = await client.GetAsync("/admin/users?page=2&pageSize=500&sort=email:asc");
        var responseBody = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, responseBody);

        var payload = JsonSerializer.Deserialize<AdminUserListDto>(responseBody, JsonOptions);
        payload.ShouldNotBeNull();
        payload!.Page.ShouldBe(2);
        payload.PageSize.ShouldBe(100); // capped at MaxPageSize
        payload.TotalCount.ShouldBe(expectedTotal);
        payload.Users.Count.ShouldBe(expectedEmails.Count);
        payload.Users.Select(user => user.Email ?? user.DisplayName ?? string.Empty)
            .ToList()
            .ShouldBe(expectedEmails);
    }

    [Fact]
    public async Task CreateUser_AssignsRoles_AndSendsOptionalEmails()
    {
        var (_, token) = await CreateAdminUserAndTokenAsync("admin-create@example.com", "AdminPass!2345", includeAdminScope: true);
        _factory.EmailSender.Clear();

        using var client = CreateAuthorizedClient(token);

        var request = new
        {
            Email = "new-user@example.com",
            DisplayName = "New User",
            EmailConfirmed = false,
            SendConfirmationEmail = true,
            SendPasswordResetEmail = true,
            Metadata = new Dictionary<string, string?>
            {
                ["displayName"] = "New User",
                ["company"] = "Acme"
            },
            Roles = new[] { "SupportAgent" }
        };

        var response = await client.PostAsJsonAsync("/admin/users", request, JsonOptions);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var createdId = ExtractUserId(response);

        _factory.EmailSender.Sent.Count.ShouldBe(2);

        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.SeedIdentityRolesAsync();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var created = await userManager.FindByIdAsync(createdId.ToString());
        created.ShouldNotBeNull();
        created!.EmailConfirmed.ShouldBeFalse();

        var detail = await client.GetFromJsonAsync<AdminUserDetailDto>($"/admin/users/{createdId:D}", JsonOptions);
        detail.ShouldNotBeNull();
        detail!.Email.ShouldBe("new-user@example.com");
        detail.Roles.ShouldContain("SupportAgent");
        detail.Metadata.ShouldContainKey("company");
        detail.Metadata["company"].ShouldBe("Acme");
    }

    [Fact]
    public async Task UpdateUser_ModifiesMetadataAndFlags()
    {
        var (_, token) = await CreateAdminUserAndTokenAsync("admin-update@example.com", "AdminPass!2345", includeAdminScope: true);
        using var client = CreateAuthorizedClient(token);

        var createResponse = await client.PostAsJsonAsync("/admin/users", new
        {
            Email = "update-user@example.com",
            Metadata = new Dictionary<string, string?>
            {
                ["displayName"] = "Before",
                ["company"] = "BeforeCo"
            }
        }, JsonOptions);

        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var createdId = ExtractUserId(createResponse);

        var detail = await client.GetFromJsonAsync<AdminUserDetailDto>($"/admin/users/{createdId:D}", JsonOptions);
        detail.ShouldNotBeNull();

        var updateRequest = new
        {
            detail!.ConcurrencyStamp,
            DisplayName = "After",
            Metadata = new Dictionary<string, string?>
            {
                ["displayName"] = "After",
                ["company"] = "AfterCo"
            },
            EmailConfirmed = true,
            TwoFactorEnabled = true,
            PhoneNumber = "+1000000000",
            PhoneNumberConfirmed = true
        };

        var updateResponse = await client.PutAsJsonAsync($"/admin/users/{createdId:D}", updateRequest, JsonOptions);
        updateResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var updated = await updateResponse.Content.ReadFromJsonAsync<AdminUserDetailDto>(JsonOptions);
        updated.ShouldNotBeNull();
        updated!.DisplayName.ShouldBe("After");
        updated.EmailConfirmed.ShouldBeTrue();
        updated.Metadata["company"].ShouldBe("AfterCo");
        updated.PhoneNumber.ShouldBe("+1000000000");
        updated.PhoneNumberConfirmed.ShouldBeTrue();
    }

    [Fact]
    public async Task LockAndUnlockUser_UpdatesLockoutState()
    {
        var (_, token) = await CreateAdminUserAndTokenAsync("admin-lock@example.com", "AdminPass!2345", includeAdminScope: true);
        using var client = CreateAuthorizedClient(token);

        var createResponse = await client.PostAsJsonAsync("/admin/users", new { Email = "lock-user@example.com" }, JsonOptions);
        var createBody = await createResponse.Content.ReadAsStringAsync();
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created, createBody);
        var createdId = ExtractUserId(createResponse);

        var lockResponse = await client.PostAsJsonAsync($"/admin/users/{createdId:D}/lock", new { Minutes = 5 }, JsonOptions);
        lockResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        using (var scope = _factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByIdAsync(createdId.ToString());
            user!.LockoutEnd.ShouldNotBeNull();
        }

        var unlockResponse = await client.PostAsync($"/admin/users/{createdId:D}/unlock", null);
        unlockResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        using (var scope = _factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByIdAsync(createdId.ToString());
            user!.LockoutEnd.ShouldBeNull();
        }
    }

    [Fact]
    public async Task ForcePasswordReset_SendsEmail()
    {
        var (_, token) = await CreateAdminUserAndTokenAsync("admin-reset@example.com", "AdminPass!2345", includeAdminScope: true);
        using var client = CreateAuthorizedClient(token);
        _factory.EmailSender.Clear();

        var createResponse = await client.PostAsJsonAsync("/admin/users", new { Email = "reset-user@example.com" }, JsonOptions);
        var createBody = await createResponse.Content.ReadAsStringAsync();
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created, createBody);
        var createdId = ExtractUserId(createResponse);

        var response = await client.PostAsync($"/admin/users/{createdId:D}/force-password-reset", null);
        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);

        _factory.EmailSender.Sent.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task ResetMfa_DisablesTwoFactor()
    {
        var (_, token) = await CreateAdminUserAndTokenAsync("admin-mfa@example.com", "AdminPass!2345", includeAdminScope: true);
        using var client = CreateAuthorizedClient(token);

        var createResponse = await client.PostAsJsonAsync("/admin/users", new { Email = "mfa-user@example.com" }, JsonOptions);
        var createBody = await createResponse.Content.ReadAsStringAsync();
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created, createBody);
        var createdId = ExtractUserId(createResponse);

        using (var scope = _factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByIdAsync(createdId.ToString());
            await userManager.SetTwoFactorEnabledAsync(user!, true);
        }

        var response = await client.PostAsync($"/admin/users/{createdId:D}/mfa/reset", null);
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        using (var scope = _factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByIdAsync(createdId.ToString());
            user!.TwoFactorEnabled.ShouldBeFalse();
        }
    }

    [Fact]
    public async Task ResendConfirmation_SendsEmail_WhenNotConfirmed()
    {
        var (_, token) = await CreateAdminUserAndTokenAsync("admin-confirm@example.com", "AdminPass!2345", includeAdminScope: true);
        using var client = CreateAuthorizedClient(token);
        _factory.EmailSender.Clear();

        var createResponse = await client.PostAsJsonAsync("/admin/users", new { Email = "confirm-user@example.com" }, JsonOptions);
        var createBody = await createResponse.Content.ReadAsStringAsync();
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created, createBody);
        var createdId = ExtractUserId(createResponse);

        var response = await client.PostAsync($"/admin/users/{createdId:D}/resend-confirmation", null);
        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);

        _factory.EmailSender.Sent.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task UpdateRoles_ReplacesAssignments()
    {
        var (_, token) = await CreateAdminUserAndTokenAsync("admin-roles@example.com", "AdminPass!2345", includeAdminScope: true);
        using var client = CreateAuthorizedClient(token);

        var createResponse = await client.PostAsJsonAsync("/admin/users", new { Email = "roles-user@example.com" }, JsonOptions);
        var createBody = await createResponse.Content.ReadAsStringAsync();
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created, createBody);
        var createdId = ExtractUserId(createResponse);

        var updateResponse = await client.PutAsJsonAsync($"/admin/users/{createdId:D}/roles", new { Roles = new[] { "SupportAgent" } }, JsonOptions);
        updateResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var rolesResponse = await client.GetFromJsonAsync<AdminUserRolesResponse>($"/admin/users/{createdId:D}/roles", JsonOptions);
        rolesResponse.ShouldNotBeNull();
        rolesResponse!.Roles.OrderBy(role => role).ToArray()
            .ShouldBe(new[] { "SupportAgent" }.OrderBy(role => role).ToArray());
    }

    [Fact]
    public async Task SoftDeleteAndRestore_TogglesDeletionFlag()
    {
        var (_, token) = await CreateAdminUserAndTokenAsync("admin-delete@example.com", "AdminPass!2345", includeAdminScope: true);
        using var client = CreateAuthorizedClient(token);

        var createResponse = await client.PostAsJsonAsync("/admin/users", new { Email = "delete-user@example.com" }, JsonOptions);
        var createBody = await createResponse.Content.ReadAsStringAsync();
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created, createBody);
        var createdId = ExtractUserId(createResponse);

        var deleteResponse = await client.DeleteAsync($"/admin/users/{createdId:D}");
        deleteResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var deletedDetail = await client.GetFromJsonAsync<AdminUserDetailDto>($"/admin/users/{createdId:D}", JsonOptions);
        deletedDetail.ShouldNotBeNull();
        deletedDetail!.IsDeleted.ShouldBeTrue();

        var restoreResponse = await client.PostAsync($"/admin/users/{createdId:D}/restore", null);
        restoreResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var restoredDetail = await client.GetFromJsonAsync<AdminUserDetailDto>($"/admin/users/{createdId:D}", JsonOptions);
        restoredDetail.ShouldNotBeNull();
        restoredDetail!.IsDeleted.ShouldBeFalse();
    }
    private async Task<(Guid UserId, string AccessToken)> CreateAdminUserAndTokenAsync(string email, string password, bool includeAdminScope)
    {
        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.SeedIdentityRolesAsync();
        var roleContext = scope.ServiceProvider.GetRequiredService<IRoleDbContext>();
        var seededRoles = await roleContext.Roles.Select(r => r.Name).ToListAsync();
        seededRoles.ShouldContain("IdentityAdmin");
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleAssignmentService = scope.ServiceProvider.GetRequiredService<IRoleAssignmentService>();

        var existing = await userManager.FindByEmailAsync(email);
        if (existing is not null)
        {
            await roleAssignmentService.AssignRolesAsync(existing.Id, new[] { "IdentityAdmin" });
        }
        else
        {
            var user = new ApplicationUser
            {
                Email = email,
                UserName = email,
                EmailConfirmed = true,
                DisplayName = "Admin Tester"
            };

            var createResult = await userManager.CreateAsync(user, password);
            createResult.Succeeded.ShouldBeTrue(createResult.Errors.FirstOrDefault()?.Description);

            await roleAssignmentService.AssignRolesAsync(user.Id, new[] { "IdentityAdmin" });
            existing = user;
        }

        var scopeValue = includeAdminScope
            ? string.Join(' ', new[] { OpenIddictConstants.Scopes.OpenId, OpenIddictConstants.Scopes.Profile, OpenIddictConstants.Scopes.Email, "identity.api", "identity.admin" })
            : string.Join(' ', new[] { OpenIddictConstants.Scopes.OpenId, OpenIddictConstants.Scopes.Profile, OpenIddictConstants.Scopes.Email, "identity.api" });

        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = false
        });

        var tokenResponse = await client.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            [OpenIddictConstants.Parameters.GrantType] = OpenIddictConstants.GrantTypes.Password,
            [OpenIddictConstants.Parameters.Username] = email,
            [OpenIddictConstants.Parameters.Password] = password,
            [OpenIddictConstants.Parameters.ClientId] = "test-client",
            [OpenIddictConstants.Parameters.ClientSecret] = "test-secret",
            [OpenIddictConstants.Parameters.Scope] = scopeValue
        }));

        var tokenPayload = await tokenResponse.Content.ReadFromJsonAsync<JsonDocument>();
        tokenResponse.StatusCode.ShouldBe(HttpStatusCode.OK, tokenPayload?.RootElement.ToString());
        tokenPayload.ShouldNotBeNull();

        var accessToken = tokenPayload!.RootElement.GetProperty("access_token").GetString();
        accessToken.ShouldNotBeNullOrWhiteSpace();

        return (existing!.Id, accessToken!);
    }

    private static async Task SeedUsersAsync(AppDbContext context, string prefix, int count)
    {
        var existing = await context.Users
            .Where(user => user.Email != null && user.Email.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .ToListAsync();

        if (existing.Count > 0)
        {
            context.Users.RemoveRange(existing);
            await context.SaveChangesAsync();
        }

        var baseTime = DateTimeOffset.UtcNow.AddMinutes(-count - 10);
        for (var index = 0; index < count; index++)
        {
            var email = $"{prefix}-{index:000}@example.com";
            var normalized = email.ToUpperInvariant();
            var user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                Email = email,
                NormalizedEmail = normalized,
                UserName = email,
                NormalizedUserName = normalized,
                DisplayName = $"Paging User {index:000}",
                EmailConfirmed = true,
                SecurityStamp = Guid.NewGuid().ToString("N"),
                ConcurrencyStamp = Guid.NewGuid().ToString("N")
            };

            SetCreatedAt(user, baseTime.AddMinutes(index));
            context.Users.Add(user);
        }

        await context.SaveChangesAsync();
    }

    private static readonly PropertyInfo CreatedAtProperty = typeof(ApplicationUser)
        .GetProperty(nameof(ApplicationUser.CreatedAt), BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)!;

    private static void SetCreatedAt(ApplicationUser user, DateTimeOffset value)
        => CreatedAtProperty.SetValue(user, value);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private HttpClient CreateAuthorizedClient(string token)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = false
        });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static Guid ExtractUserId(HttpResponseMessage response)
    {
        var location = response.Headers.Location ?? throw new InvalidOperationException("Response did not include a Location header.");
        var path = location.IsAbsoluteUri ? location.AbsolutePath : location.OriginalString;
        var segments = path.TrimEnd('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
        {
            throw new InvalidOperationException("Location header did not contain an identifier segment.");
        }

        var idSegment = segments[^1];
        return Guid.Parse(idSegment);
    }

    private sealed record AdminUserListDto(int Page, int PageSize, int TotalCount, List<AdminUserSummaryDto> Users);

    private sealed record AdminUserSummaryDto(Guid Id, string? Email, string? DisplayName, bool EmailConfirmed, bool IsLockedOut, DateTimeOffset CreatedAt, bool MfaEnabled, List<string> Roles, bool IsDeleted);

    private sealed record AdminUserDetailDto(Guid Id, string? Email, bool EmailConfirmed, string? DisplayName, DateTimeOffset CreatedAt, bool LockoutEnabled, bool IsLockedOut, DateTimeOffset? LockoutEnd, bool TwoFactorEnabled, bool PhoneNumberConfirmed, string? PhoneNumber, Dictionary<string, string?> Metadata, string ConcurrencyStamp, List<string> Roles, List<AdminUserExternalLoginDto> ExternalLogins, bool AuthenticatorConfigured, bool IsDeleted);

    private sealed record AdminUserExternalLoginDto(string Provider, string DisplayName, string Key);

    private sealed record AdminUserRolesResponse(IReadOnlyList<string> Roles);
}
