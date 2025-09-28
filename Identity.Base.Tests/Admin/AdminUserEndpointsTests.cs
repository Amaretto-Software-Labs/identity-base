using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Identity.Base.Identity;
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
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<AdminUserListDto>(JsonOptions);
        payload.Should().NotBeNull();
        payload!.TotalCount.Should().BeGreaterThan(0);
        payload.Users.Should().Contain(item => string.Equals(item.Email, email, StringComparison.OrdinalIgnoreCase));
        var adminEntry = payload.Users.First(item => string.Equals(item.Email, email, StringComparison.OrdinalIgnoreCase));
        adminEntry.Roles.Should().Contain("IdentityAdmin");
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
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<AdminUserDetailDto>(JsonOptions);
        payload.Should().NotBeNull();
        payload!.Email.Should().Be(email);
        payload.Roles.Should().Contain("IdentityAdmin");
        payload.Metadata.Should().NotBeNull();
    }

    [Fact]
    public async Task ListUsers_ReturnsForbidden_WhenScopeMissing()
    {
        const string email = "admin-noscope@example.com";
        const string password = "AdminPass!2345";

        var (_, token) = await CreateAdminUserAndTokenAsync(email, password, includeAdminScope: false);

        using var client = CreateAuthorizedClient(token);

        var response = await client.GetAsync("/admin/users");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
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
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var createdId = ExtractUserId(response);

        _factory.EmailSender.Sent.Should().HaveCount(2);

        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.SeedIdentityRolesAsync();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var created = await userManager.FindByIdAsync(createdId.ToString());
        created.Should().NotBeNull();
        created!.EmailConfirmed.Should().BeFalse();

        var detail = await client.GetFromJsonAsync<AdminUserDetailDto>($"/admin/users/{createdId:D}", JsonOptions);
        detail.Should().NotBeNull();
        detail!.Email.Should().Be("new-user@example.com");
        detail.Roles.Should().Contain("SupportAgent");
        detail.Metadata.Should().ContainKey("company").WhoseValue.Should().Be("Acme");
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

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var createdId = ExtractUserId(createResponse);

        var detail = await client.GetFromJsonAsync<AdminUserDetailDto>($"/admin/users/{createdId:D}", JsonOptions);
        detail.Should().NotBeNull();

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
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var updated = await updateResponse.Content.ReadFromJsonAsync<AdminUserDetailDto>(JsonOptions);
        updated.Should().NotBeNull();
        updated!.DisplayName.Should().Be("After");
        updated.EmailConfirmed.Should().BeTrue();
        updated.Metadata["company"].Should().Be("AfterCo");
        updated.PhoneNumber.Should().Be("+1000000000");
        updated.PhoneNumberConfirmed.Should().BeTrue();
    }

    [Fact]
    public async Task LockAndUnlockUser_UpdatesLockoutState()
    {
        var (_, token) = await CreateAdminUserAndTokenAsync("admin-lock@example.com", "AdminPass!2345", includeAdminScope: true);
        using var client = CreateAuthorizedClient(token);

        var createResponse = await client.PostAsJsonAsync("/admin/users", new { Email = "lock-user@example.com" }, JsonOptions);
        var createBody = await createResponse.Content.ReadAsStringAsync();
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created, createBody);
        var createdId = ExtractUserId(createResponse);

        var lockResponse = await client.PostAsJsonAsync($"/admin/users/{createdId:D}/lock", new { Minutes = 5 }, JsonOptions);
        lockResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using (var scope = _factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByIdAsync(createdId.ToString());
            user!.LockoutEnd.Should().NotBeNull();
        }

        var unlockResponse = await client.PostAsync($"/admin/users/{createdId:D}/unlock", null);
        unlockResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using (var scope = _factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByIdAsync(createdId.ToString());
            user!.LockoutEnd.Should().BeNull();
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
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created, createBody);
        var createdId = ExtractUserId(createResponse);

        var response = await client.PostAsync($"/admin/users/{createdId:D}/force-password-reset", null);
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        _factory.EmailSender.Sent.Should().ContainSingle();
    }

    [Fact]
    public async Task ResetMfa_DisablesTwoFactor()
    {
        var (_, token) = await CreateAdminUserAndTokenAsync("admin-mfa@example.com", "AdminPass!2345", includeAdminScope: true);
        using var client = CreateAuthorizedClient(token);

        var createResponse = await client.PostAsJsonAsync("/admin/users", new { Email = "mfa-user@example.com" }, JsonOptions);
        var createBody = await createResponse.Content.ReadAsStringAsync();
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created, createBody);
        var createdId = ExtractUserId(createResponse);

        using (var scope = _factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByIdAsync(createdId.ToString());
            await userManager.SetTwoFactorEnabledAsync(user!, true);
        }

        var response = await client.PostAsync($"/admin/users/{createdId:D}/mfa/reset", null);
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using (var scope = _factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByIdAsync(createdId.ToString());
            user!.TwoFactorEnabled.Should().BeFalse();
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
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created, createBody);
        var createdId = ExtractUserId(createResponse);

        var response = await client.PostAsync($"/admin/users/{createdId:D}/resend-confirmation", null);
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        _factory.EmailSender.Sent.Should().ContainSingle();
    }

    [Fact]
    public async Task UpdateRoles_ReplacesAssignments()
    {
        var (_, token) = await CreateAdminUserAndTokenAsync("admin-roles@example.com", "AdminPass!2345", includeAdminScope: true);
        using var client = CreateAuthorizedClient(token);

        var createResponse = await client.PostAsJsonAsync("/admin/users", new { Email = "roles-user@example.com" }, JsonOptions);
        var createBody = await createResponse.Content.ReadAsStringAsync();
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created, createBody);
        var createdId = ExtractUserId(createResponse);

        var updateResponse = await client.PutAsJsonAsync($"/admin/users/{createdId:D}/roles", new { Roles = new[] { "SupportAgent" } }, JsonOptions);
        updateResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var rolesResponse = await client.GetFromJsonAsync<AdminUserRolesResponse>($"/admin/users/{createdId:D}/roles", JsonOptions);
        rolesResponse.Should().NotBeNull();
        rolesResponse!.Roles.Should().BeEquivalentTo(new[] { "SupportAgent" });
    }

    [Fact]
    public async Task SoftDeleteAndRestore_TogglesDeletionFlag()
    {
        var (_, token) = await CreateAdminUserAndTokenAsync("admin-delete@example.com", "AdminPass!2345", includeAdminScope: true);
        using var client = CreateAuthorizedClient(token);

        var createResponse = await client.PostAsJsonAsync("/admin/users", new { Email = "delete-user@example.com" }, JsonOptions);
        var createBody = await createResponse.Content.ReadAsStringAsync();
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created, createBody);
        var createdId = ExtractUserId(createResponse);

        var deleteResponse = await client.DeleteAsync($"/admin/users/{createdId:D}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var deletedDetail = await client.GetFromJsonAsync<AdminUserDetailDto>($"/admin/users/{createdId:D}", JsonOptions);
        deletedDetail.Should().NotBeNull();
        deletedDetail!.IsDeleted.Should().BeTrue();

        var restoreResponse = await client.PostAsync($"/admin/users/{createdId:D}/restore", null);
        restoreResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var restoredDetail = await client.GetFromJsonAsync<AdminUserDetailDto>($"/admin/users/{createdId:D}", JsonOptions);
        restoredDetail.Should().NotBeNull();
        restoredDetail!.IsDeleted.Should().BeFalse();
    }
    private async Task<(Guid UserId, string AccessToken)> CreateAdminUserAndTokenAsync(string email, string password, bool includeAdminScope)
    {
        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.SeedIdentityRolesAsync();
        var roleContext = scope.ServiceProvider.GetRequiredService<IRoleDbContext>();
        var seededRoles = await roleContext.Roles.Select(r => r.Name).ToListAsync();
        seededRoles.Should().Contain("IdentityAdmin");
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
            createResult.Succeeded.Should().BeTrue(createResult.Errors.FirstOrDefault()?.Description);

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
        tokenResponse.StatusCode.Should().Be(HttpStatusCode.OK, tokenPayload?.RootElement.ToString());
        tokenPayload.Should().NotBeNull();

        var accessToken = tokenPayload!.RootElement.GetProperty("access_token").GetString();
        accessToken.Should().NotBeNullOrWhiteSpace();

        return (existing!.Id, accessToken!);
    }

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
